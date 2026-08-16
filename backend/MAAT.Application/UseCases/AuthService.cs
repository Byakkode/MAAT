using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Application.Security;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Application.UseCases;

public class AuthService(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ICompromisedPasswordChecker compromisedPasswordChecker,
    IJwtTokenService jwtTokenService,
    IEmailSender emailSender)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan EmailVerificationTokenLifetime = TimeSpan.FromHours(24);
    private const int MinimumPasswordLength = 12;

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (request.Password.Length < MinimumPasswordLength)
        {
            throw new WeakPasswordException($"Le mot de passe doit contenir au moins {MinimumPasswordLength} caractères.");
        }

        if (await compromisedPasswordChecker.IsCompromisedAsync(request.Password, ct))
        {
            throw new CompromisedPasswordException("Ce mot de passe a été compromis lors d'une fuite de données connue. Choisissez-en un autre.");
        }

        // Hachage exécuté systématiquement, y compris sur le chemin doublon ci-dessous :
        // un retour anticipé qui l'aurait sauté aurait créé un écart de latence
        // (~250 ms, cf. section 1 de la spec) exploitable pour détecter qu'une adresse
        // est déjà enregistrée. Même parade que le hash factice de LoginAsync.
        var passwordHash = passwordHasher.Hash(request.Password);

        var existingUser = await userRepository.FindByEmailAsync(email, ct);
        if (existingUser is not null)
        {
            await emailSender.SendDuplicateRegistrationAttemptNoticeAsync(email, ct);
            return;
        }

        var company = new Company(request.CompanyName, request.SectorCode, request.SizeRange, request.Region, request.Siret);
        await companyRepository.AddAsync(company, ct);

        var user = new User(email, passwordHash, company.Id, UserRole.Admin);
        await userRepository.AddAsync(user, ct);

        await IssueAndSendVerificationEmailAsync(user, email, ct);
    }

    // docs/specs/auth-securite-rgpd.md, section 1 : renvoi de l'e-mail de vérification. Même
    // anti-énumération que RegisterAsync — aucune branche visible de l'extérieur : le
    // contrôleur renvoie une réponse identique que l'adresse existe, soit déjà vérifiée, ou
    // non. Le jeton précédent est invalidé avant l'émission du nouveau (RevokeAllUnconsumed...)
    // pour qu'un seul jeton reste utilisable à la fois.
    public async Task ResendVerificationEmailAsync(string emailInput, CancellationToken ct)
    {
        var email = emailInput.Trim().ToLowerInvariant();
        var user = await userRepository.FindByEmailAsync(email, ct);

        if (user is null || user.EmailVerified)
        {
            return;
        }

        await emailVerificationTokenRepository.RevokeAllUnconsumedForUserAsync(user.Id, ct);
        await IssueAndSendVerificationEmailAsync(user, email, ct);
    }

    private async Task IssueAndSendVerificationEmailAsync(User user, string email, CancellationToken ct)
    {
        var verificationToken = SecureTokenGenerator.Generate();
        await emailVerificationTokenRepository.IssueAsync(user.Id, verificationToken, DateTimeOffset.UtcNow.Add(EmailVerificationTokenLifetime), ct);

        await unitOfWork.SaveChangesAsync(ct);

        await emailSender.SendVerificationEmailAsync(email, verificationToken, ct);
    }

    public async Task<AuthTokensResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.FindByEmailAsync(email, ct);

        var hashToVerify = user?.PasswordHash ?? passwordHasher.DummyHash;
        var passwordValid = passwordHasher.Verify(request.Password, hashToVerify);

        if (user is null || !passwordValid)
        {
            throw new InvalidCredentialsException();
        }

        user.LastLogin = DateTimeOffset.UtcNow;

        return await IssueTokenPairAsync(user, ct);
    }

    public async Task<AuthTokensResult> RefreshAsync(string refreshTokenPlaintext, CancellationToken ct)
    {
        var token = await refreshTokenRepository.FindByPlaintextAsync(refreshTokenPlaintext, ct);

        if (token is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (token.RevokedAt is not null)
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(token.UserId, ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new RefreshTokenReuseDetectedException();
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await userRepository.GetByIdAsync(token.UserId, ct)
            ?? throw new InvalidRefreshTokenException();

        await refreshTokenRepository.RevokeAsync(token, ct);

        return await IssueTokenPairAsync(user, ct);
    }

    public async Task LogoutAsync(string refreshTokenPlaintext, CancellationToken ct)
    {
        var token = await refreshTokenRepository.FindByPlaintextAsync(refreshTokenPlaintext, ct);
        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        await refreshTokenRepository.RevokeAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task VerifyEmailAsync(string verificationTokenPlaintext, CancellationToken ct)
    {
        var token = await emailVerificationTokenRepository.FindByPlaintextAsync(verificationTokenPlaintext, ct);
        if (token is null || token.ConsumedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidVerificationTokenException();
        }

        var user = await userRepository.GetByIdAsync(token.UserId, ct)
            ?? throw new InvalidVerificationTokenException();

        user.EmailVerified = true;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;

        await emailVerificationTokenRepository.MarkConsumedAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<AuthTokensResult> IssueTokenPairAsync(User user, CancellationToken ct)
    {
        var (accessToken, accessExpiresAt) = jwtTokenService.CreateAccessToken(user);

        var refreshPlaintext = SecureTokenGenerator.Generate();
        var refreshExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime);
        await refreshTokenRepository.IssueAsync(user.Id, refreshPlaintext, refreshExpiresAt, ct);

        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokensResult(accessToken, accessExpiresAt, refreshPlaintext, refreshExpiresAt);
    }
}
