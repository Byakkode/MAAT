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
    ISubscriptionRepository subscriptionRepository,
    IRseIndicatorsRepository rseIndicatorsRepository,
    IVsmeStatementRepository vsmeStatementRepository,
    ICompanySiteRepository companySiteRepository,
    IActionItemProgressRepository actionItemProgressRepository,
    IActionItemChangeRepository actionItemChangeRepository,
    ISupportTicketRepository supportTicketRepository,
    ICompanyLogoRepository companyLogoRepository,
    IPaymentGateway paymentGateway,
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
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        var rseIndicators = await rseIndicatorsRepository.ListByCompanyAsync(currentUser.CompanyId, ct);
        var vsmeStatements = await vsmeStatementRepository.ListAsync(currentUser.CompanyId, ct);
        var companySites = await companySiteRepository.ListAsync(currentUser.CompanyId, ct);
        var actionItemProgress = await actionItemProgressRepository.ListByCompanyAsync(currentUser.CompanyId, ct);
        var actionItemChanges = await actionItemChangeRepository.ListByCompanyAsync(currentUser.CompanyId, ct);
        var supportTickets = await supportTicketRepository.GetAllByCompanyAsync(currentUser.CompanyId, ct);
        var companyLogo = await companyLogoRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);

        return new AccountExportResult(
            new UserExport(user.Id, user.Email, user.Role, user.EmailVerified, user.EmailVerifiedAt, user.LastLogin, user.CreatedAt),
            new CompanyExport(company.Id, company.Name, company.SectorCode, company.SizeRange, company.Region, company.Siret, company.CreatedAt),
            [.. diagnostics.Select(d => new DiagnosticExport(d.Id, d.Status, d.GlobalScore, d.CreatedAt, d.CompletedAt))],
            [.. responses.Select(r => new ResponseExport(r.Id, r.DiagnosticId, r.QuestionId, r.Value, r.AnsweredAt))],
            [.. domainScores.Select(ds => new DomainScoreExport(ds.DiagnosticId, ds.Domain, ds.Score, ds.SectorWeight))],
            [.. diagnosticRecommendations.Select(dr => new DiagnosticRecommendationExport(dr.DiagnosticId, dr.RecommendationId, dr.IsCompleted, dr.CompletedAt, dr.PriorityRank))],
            [.. reports.Select(r => new ReportExport(r.Id, r.DiagnosticId, r.Format, r.GeneratedAt, r.GeneratedByUserId))],
            subscription is null
                ? null
                : new SubscriptionExport(subscription.Plan, subscription.BillingPeriod, subscription.Status, subscription.CreatedAt, subscription.UpdatedAt),
            [.. rseIndicators.Select(ToExport)],
            [.. vsmeStatements.Select(ToExport)],
            [.. companySites.Select(s => new CompanySiteExport(
                s.Id, s.Name, s.Address, s.Tenure, s.Latitude, s.Longitude, s.GeocodedLabel,
                s.InOrNearSensitiveArea, s.SensitiveAreaName, s.SensitiveAreasCheckedAt, s.DetectedSensitiveAreas,
                s.CreatedAt, s.UpdatedAt))],
            [.. actionItemProgress.Select(p => new ActionItemProgressExport(
                p.Id, p.DiagnosticId, p.RecommendationCode, p.Status, p.AssignedTo, p.DueDate, p.Notes, p.UpdatedAt))],
            [.. actionItemChanges.Select(c => new ActionItemChangeExport(
                c.Id, c.DiagnosticId, c.RecommendationCode, c.Field, c.OldValue, c.NewValue, c.ChangedByUserId, c.ChangedAt))],
            [.. supportTickets.Select(t => new SupportTicketExport(
                t.Id, t.UserId, t.GithubIssueNumber, t.GithubIssueUrl, t.Title, t.Description, t.TicketType, t.CreatedAt))],
            companyLogo is null ? null : new CompanyLogoExport("image/png", companyLogo.PngContent, companyLogo.UpdatedAt));
    }

    private static RseIndicatorsExport ToExport(Domain.Entities.RseIndicators r) => new()
    {
        Id = r.Id,
        Year = r.Year,
        Co2EmissionsTons = r.Co2EmissionsTons,
        EnergyConsumptionKwh = r.EnergyConsumptionKwh,
        RenewableEnergyPct = r.RenewableEnergyPct,
        WaterConsumptionM3 = r.WaterConsumptionM3,
        WasteTons = r.WasteTons,
        RecyclingRatePct = r.RecyclingRatePct,
        ElectricityRenewableMwh = r.ElectricityRenewableMwh,
        ElectricityNonRenewableMwh = r.ElectricityNonRenewableMwh,
        FuelsRenewableMwh = r.FuelsRenewableMwh,
        FuelsNonRenewableMwh = r.FuelsNonRenewableMwh,
        Scope1Tco2e = r.Scope1Tco2e,
        Scope2LocationTco2e = r.Scope2LocationTco2e,
        WaterWithdrawalM3 = r.WaterWithdrawalM3,
        WaterConsumptionStressM3 = r.WaterConsumptionStressM3,
        HazardousWasteTons = r.HazardousWasteTons,
        NonHazardousWasteTons = r.NonHazardousWasteTons,
        EmployeeCountFte = r.EmployeeCountFte,
        TurnoverRatePct = r.TurnoverRatePct,
        TrainingHoursPerEmployee = r.TrainingHoursPerEmployee,
        WorkAccidentRate = r.WorkAccidentRate,
        GenderEqualityIndex = r.GenderEqualityIndex,
        PermanentContractPct = r.PermanentContractPct,
        PermanentEmployees = r.PermanentEmployees,
        TemporaryEmployees = r.TemporaryEmployees,
        FemaleEmployees = r.FemaleEmployees,
        MaleEmployees = r.MaleEmployees,
        OtherGenderEmployees = r.OtherGenderEmployees,
        RecordableAccidents = r.RecordableAccidents,
        HoursWorked = r.HoursWorked,
        WorkFatalities = r.WorkFatalities,
        GenderPayGapPct = r.GenderPayGapPct,
        CollectiveBargainingPct = r.CollectiveBargainingPct,
        LocalSuppliersPct = r.LocalSuppliersPct,
        RseAssessedSuppliersPct = r.RseAssessedSuppliersPct,
        ActiveSuppliersCount = r.ActiveSuppliersCount,
        RevenueEur = r.RevenueEur,
        RseInvestmentEur = r.RseInvestmentEur,
        ExportRevenuePct = r.ExportRevenuePct,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static VsmeStatementExport ToExport(Domain.Entities.VsmeStatement s) => new()
    {
        Id = s.Id,
        Year = s.Year,
        ReportingBasis = s.ReportingBasis,
        LegalForm = s.LegalForm,
        TotalAssetsEur = s.TotalAssetsEur,
        PrimaryCountry = s.PrimaryCountry,
        EmployeeCountUnit = s.EmployeeCountUnit,
        OmittedDisclosures = [.. s.OmittedDisclosures],
        Subsidiaries = [.. s.Subsidiaries.Select(x => new VsmeSubsidiaryExport(x.Name, x.RegisteredAddress))],
        Certifications = [.. s.Certifications.Select(x => new VsmeCertificationExport(x.Name, x.Issuer, x.ObtainedOn, x.Rating))],
        HasPractices = s.HasPractices,
        HasPolicies = s.HasPolicies,
        PoliciesPublic = s.PoliciesPublic,
        HasFutureInitiatives = s.HasFutureInitiatives,
        HasTargets = s.HasTargets,
        PracticesDescription = s.PracticesDescription,
        CoveredTopics = [.. s.CoveredTopics],
        PollutionReportingApplicable = s.PollutionReportingApplicable,
        PollutionReportUrl = s.PollutionReportUrl,
        Pollutants = [.. s.Pollutants.Select(x => new VsmePollutantExport(x.Name, x.Medium, x.Quantity, x.Unit))],
        CircularEconomyApplied = s.CircularEconomyApplied,
        CircularEconomyDescription = s.CircularEconomyDescription,
        MaterialFlowsDescription = s.MaterialFlowsDescription,
        EmployeesByCountry = [.. s.EmployeesByCountry.Select(x => new VsmeCountryHeadcountExport(x.Country, x.Employees))],
        MinimumWageMet = s.MinimumWageMet,
        CorruptionConvictions = s.CorruptionConvictions,
        CorruptionFinesEur = s.CorruptionFinesEur,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

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

        // docs/specs/abonnement.md, section 6 : l'entreprise disparaît, son abonnement payant
        // doit cesser d'être facturé. Résilié AVANT la purge, hors transaction : si le
        // prestataire refuse, la suppression échoue entière plutôt que de laisser un
        // prélèvement courir pour une entreprise qui n'existe plus.
        if (isLastAdmin)
        {
            var subscription = await subscriptionRepository.FindByCompanyIdAsync(companyId, ct);
            if (subscription?.StripeSubscriptionId is { } providerSubscriptionId)
            {
                await paymentGateway.CancelSubscriptionAsync(providerSubscriptionId, ct);
            }
        }

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
