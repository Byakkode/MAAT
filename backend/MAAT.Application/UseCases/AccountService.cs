using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;

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
    IPasswordHasher passwordHasher,
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

    public async Task DeleteAccountAsync(string passwordConfirmation, CancellationToken ct)
    {
        await GetCurrentUserOrThrowAsync(passwordConfirmation, ct);

        var companyId = currentUser.CompanyId;

        await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            // Ordre impératif : Diagnostic avant User. Report.generated_by_user_id
            // référence User en ON DELETE RESTRICT ; supprimer les Diagnostic d'abord
            // fait cascader la suppression des Report (entre autres) avant qu'on
            // supprime les User, sans quoi la suppression des User échouerait.
            await diagnosticRepository.DeleteAllForCurrentCompanyAsync(innerCt);
            await userRepository.DeleteAllForCompanyAsync(companyId, innerCt);
            await companyRepository.DeleteAsync(companyId, innerCt);
        }, ct);
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
