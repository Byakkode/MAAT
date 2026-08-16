using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct);

    Task<EmailVerificationToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct);

    Task MarkConsumedAsync(EmailVerificationToken token, CancellationToken ct);

    // Renvoi de vérification (auth-securite-rgpd.md, section 1) : l'ancien jeton doit devenir
    // inutilisable dès qu'un nouveau est émis, sans quoi les deux resteraient valides en
    // parallèle jusqu'à leur expiration naturelle. Exécuté immédiatement en base (comme
    // UserRepository.DeleteAllForCompanyAsync), pas via le suivi de changements : ces lignes
    // ne sont jamais chargées en mémoire par ailleurs.
    Task RevokeAllUnconsumedForUserAsync(Guid userId, CancellationToken ct);
}
