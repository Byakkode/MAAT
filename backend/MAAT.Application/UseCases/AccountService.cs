using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Application.Security;
using MAAT.Domain.Enums;

namespace MAAT.Application.UseCases;

// docs/specs/auth-securite-rgpd.md, section 6 : droit d'accès/portabilité (export) et
// droit à l'effacement (suppression). Les deux exigent une reconfirmation du mot de
// passe. Tourne dans le contexte de la requête HTTP de l'utilisateur qui agit sur son
// propre compte : les dépôts à portée d'entreprise (ICurrentUserContext) conviennent
// donc tels quels (docs/adr/0005) — aucun contexte système n'est nécessaire ici.
public class AccountService(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IDiagnosticRepository diagnosticRepository,
    IResponseRepository responseRepository,
    IDomainScoreRepository domainScoreRepository,
    IDiagnosticRecommendationRepository diagnosticRecommendationRepository,
    IReportRepository reportRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ICompromisedPasswordChecker compromisedPasswordChecker,
    ICurrentUserContext currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<AccountExportResult> ExportAsync(string passwordConfirmation, CancellationToken ct)
    {
        var user = await GetCurrentUserOrThrowAsync(passwordConfirmation, ct);
        var company = await companyRepository.GetByIdAsync(currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException("Entreprise du principal authentifié introuvable.");

        var diagnostics = await diagnosticRepository.FindAllForCurrentCompanyAsync(ct);
        var responses = await responseRepository.FindAllForCurrentCompanyAsync(ct);
        var domainScores = await domainScoreRepository.FindAllForCurrentCompanyAsync(ct);
        var diagnosticRecommendations = await diagnosticRecommendationRepository.FindAllForCurrentCompanyAsync(ct);
        var reports = await reportRepository.FindAllForCurrentCompanyAsync(ct);

        return new AccountExportResult(
            new UserExport(user.Id, user.Email, user.Role, user.EmailVerified, user.EmailVerifiedAt, user.LastLogin, user.CreatedAt),
            new CompanyExport(company.Id, company.Name, company.SectorCode, company.SizeRange, company.Region, company.Siret, company.CreatedAt),
            [.. diagnostics.Select(d => new DiagnosticExport(d.Id, d.Status, d.GlobalScore, d.CreatedAt, d.CompletedAt))],
            [.. responses.Select(r => new ResponseExport(r.Id, r.DiagnosticId, r.QuestionId, r.Value, r.AnsweredAt))],
            [.. domainScores.Select(ds => new DomainScoreExport(ds.DiagnosticId, ds.Domain, ds.Score, ds.SectorWeight))],
            [.. diagnosticRecommendations.Select(dr => new DiagnosticRecommendationExport(dr.DiagnosticId, dr.RecommendationId, dr.IsCompleted, dr.CompletedAt, dr.PriorityRank))],
            [.. reports.Select(r => new ReportExport(r.Id, r.DiagnosticId, r.Format, r.GeneratedAt, r.GeneratedByUserId))]);
    }

    // docs/specs/coquille-et-compte.md, section 6 : DELETE /api/me ne supprime l'entreprise que
    // si l'appelant en est le dernier Admin — dans tous les autres cas (Viewer, User, ou Admin
    // alors qu'un autre Admin existe), seul son propre compte disparaît. Corrige une élévation
    // de privilège : la version précédente supprimait toujours l'entreprise entière, y compris
    // pour un Viewer, sans condition de rôle ni comptage d'administrateurs.
    public async Task<bool> DeleteAccountAsync(string passwordConfirmation, CancellationToken ct)
    {
        var user = await GetCurrentUserOrThrowAsync(passwordConfirmation, ct);
        var companyId = currentUser.CompanyId;

        var isLastAdmin = user.Role == UserRole.Admin && await userRepository.CountAdminsForCompanyAsync(companyId, ct) == 1;

        await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            if (isLastAdmin)
            {
                // Ordre impératif : Diagnostic avant User. Report.generated_by_user_id
                // référence User en ON DELETE RESTRICT ; supprimer les Diagnostic d'abord
                // fait cascader la suppression des Report (entre autres) avant qu'on
                // supprime les User, sans quoi la suppression des User échouerait.
                await diagnosticRepository.DeleteAllForCurrentCompanyAsync(innerCt);
                await userRepository.DeleteAllForCompanyAsync(companyId, innerCt);
                await companyRepository.DeleteAsync(companyId, innerCt);
            }
            else
            {
                // Seul ce compte disparaît : l'entreprise, les autres comptes et les
                // diagnostics restent intacts. Les rapports générés par CE compte doivent
                // disparaître avant lui, même contrainte ON DELETE RESTRICT que ci-dessus —
                // jamais ceux des autres comptes.
                await reportRepository.DeleteAllGeneratedByUserAsync(user.Id, innerCt);
                await userRepository.DeleteAsync(user.Id, innerCt);
            }
        }, ct);

        return isLastAdmin;
    }

    // docs/specs/coquille-et-compte.md, section 5 : "Un changement réussi invalide les autres
    // sessions." La session courante (celle qui vient de fournir l'ancien mot de passe avec
    // succès) reste connectée — currentRefreshTokenPlaintext identifie laquelle préserver ;
    // absent ou introuvable (cookie manquant, jeton déjà expiré), tout est révoqué par prudence
    // plutôt que de risquer d'en préserver un mauvais.
    public async Task ChangePasswordAsync(string currentPassword, string newPassword, string? currentRefreshTokenPlaintext, CancellationToken ct)
    {
        var user = await GetCurrentUserOrThrowAsync(currentPassword, ct);

        if (newPassword.Length < PasswordPolicy.MinimumLength)
        {
            throw new WeakPasswordException($"Le mot de passe doit contenir au moins {PasswordPolicy.MinimumLength} caractères.");
        }

        if (await compromisedPasswordChecker.IsCompromisedAsync(newPassword, ct))
        {
            throw new CompromisedPasswordException("Ce mot de passe a été compromis lors d'une fuite de données connue. Choisissez-en un autre.");
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);

        var currentToken = currentRefreshTokenPlaintext is not null
            ? await refreshTokenRepository.FindByPlaintextAsync(currentRefreshTokenPlaintext, ct)
            : null;

        if (currentToken is not null)
        {
            await refreshTokenRepository.RevokeAllActiveForUserExceptAsync(user.Id, currentToken.Id, ct);
        }
        else
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Domain.Entities.User> GetCurrentUserOrThrowAsync(string passwordConfirmation, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new InvalidOperationException("Utilisateur du principal authentifié introuvable.");

        if (!passwordHasher.Verify(passwordConfirmation, user.PasswordHash))
        {
            throw new PasswordConfirmationFailedException();
        }

        return user;
    }
}
