using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/norme-volontaire.md, section 4 : déclarations de l'exercice, sites de l'entreprise
// et complétude du module de base. Toujours l'entreprise du principal authentifié. Le rôle
// (Admin ou User pour écrire) est exigé par le contrôleur ; l'offre est vérifiée ici, avec le
// même droit que les indicateurs chiffrés, dont ces données sont le prolongement.
public class VsmeService(
    IVsmeStatementRepository statementRepository,
    ICompanySiteRepository siteRepository,
    IRseIndicatorsRepository indicatorsRepository,
    ICompanyRepository companyRepository,
    IGeocoder geocoder,
    ISensitiveAreaLocator sensitiveAreaLocator,
    CurrentPlanService currentPlan,
    ICurrentUserContext currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const int MaxSites = 50;
    public const int MaxTextLength = 2000;
    public const int MaxShortTextLength = 200;
    public const int MaxListItems = 50;

    public Task<VsmeStatement?> GetStatementAsync(int year, CancellationToken ct) =>
        statementRepository.FindAsync(currentUser.CompanyId, year, ct);

    public async Task<VsmeStatement> SaveStatementAsync(int year, VsmeStatementValues values, CancellationToken ct)
    {
        await currentPlan.EnsureAsync(e => e.CanEditIndicators, SubscriptionPlan.Essential, ct);
        ValidateYear(year);
        Validate(values);

        var now = timeProvider.GetUtcNow();
        var statement = await statementRepository.FindAsync(currentUser.CompanyId, year, ct);
        if (statement is null)
        {
            statement = new VsmeStatement(currentUser.CompanyId, year, now);
            await statementRepository.AddAsync(statement, ct);
        }

        statement.Update(values, now);
        await unitOfWork.SaveChangesAsync(ct);
        return statement;
    }

    // Lecture ouverte quelle que soit l'offre, comme les indicateurs : une entreprise revenue sur
    // Starter voit encore ce qu'elle avait saisi (abonnement.md, section 8).
    public async Task<VsmeCompletenessResult> GetCompletenessAsync(int year, CancellationToken ct)
    {
        var companyId = currentUser.CompanyId;
        var company = await companyRepository.GetByIdAsync(companyId, ct)
            ?? throw new InvalidOperationException("Entreprise du principal authentifié introuvable.");

        return VsmeCompleteness.Evaluate(
            await statementRepository.FindAsync(companyId, year, ct),
            await indicatorsRepository.GetByCompanyAndYearAsync(companyId, year, ct),
            await siteRepository.ListAsync(companyId, ct),
            company.SizeRange);
    }

    public Task<List<CompanySite>> ListSitesAsync(CancellationToken ct) =>
        siteRepository.ListAsync(currentUser.CompanyId, ct);

    public async Task<CompanySite> CreateSiteAsync(CompanySiteDetails details, CancellationToken ct)
    {
        await currentPlan.EnsureAsync(e => e.CanEditIndicators, SubscriptionPlan.Essential, ct);
        Validate(details);

        if (await siteRepository.CountAsync(currentUser.CompanyId, ct) >= MaxSites)
        {
            throw new SiteLimitReachedException(MaxSites);
        }

        var site = new CompanySite(currentUser.CompanyId, details, timeProvider.GetUtcNow());
        await LocateAsync(site, ct);
        await siteRepository.AddAsync(site, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return site;
    }

    // Null : aucun site de cet identifiant pour l'entreprise (404).
    public async Task<CompanySite?> UpdateSiteAsync(Guid siteId, CompanySiteDetails details, CancellationToken ct)
    {
        await currentPlan.EnsureAsync(e => e.CanEditIndicators, SubscriptionPlan.Essential, ct);
        Validate(details);

        var site = await siteRepository.FindAsync(currentUser.CompanyId, siteId, ct);
        if (site is null)
        {
            return null;
        }

        // Nouvelle tentative aussi quand l'adresse n'a pas changé mais n'avait pas été localisée,
        // ou que la recherche de zones sensibles n'avait pas abouti : un service était peut-être
        // indisponible lors de l'enregistrement précédent.
        if (site.Update(details, timeProvider.GetUtcNow()) || !site.IsGeolocated)
        {
            await LocateAsync(site, ct);
        }
        else if (!site.IsCheckedForSensitiveAreas)
        {
            await CheckSensitiveAreasAsync(site, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return site;
    }

    // Pas de contrôle d'offre : retirer un site reste possible après un retour sur Starter, comme
    // retirer son logo. Idempotent.
    public async Task DeleteSiteAsync(Guid siteId, CancellationToken ct)
    {
        var site = await siteRepository.FindAsync(currentUser.CompanyId, siteId, ct);
        if (site is null)
        {
            return;
        }

        siteRepository.Remove(site);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task LocateAsync(CompanySite site, CancellationToken ct)
    {
        var result = await geocoder.GeocodeAsync(site.Address, ct);
        if (result is null)
        {
            site.ClearLocation();
            return;
        }

        site.Locate(result.Latitude, result.Longitude, result.Label);
        await CheckSensitiveAreasAsync(site, ct);
    }

    // ADR 0014 : zones sensibles autour du site, pour répondre à B5 quand l'utilisateur ne l'a
    // pas fait. Toujours effectuée, même s'il a répondu : sa réponse prime, mais l'écran peut
    // lui montrer ce que la base publique indique.
    private async Task CheckSensitiveAreasAsync(CompanySite site, CancellationToken ct)
    {
        if (site.Latitude is not { } latitude || site.Longitude is not { } longitude)
        {
            return;
        }

        var areas = await sensitiveAreaLocator.FindNearAsync(latitude, longitude, ct);
        if (areas is not null)
        {
            site.RecordSensitiveAreaCheck(SensitiveAreaSummary.Format(areas), timeProvider.GetUtcNow());
        }
    }

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 2100)
        {
            throw new InvalidVsmeDataException("Année invalide.");
        }
    }

    private static void Validate(VsmeStatementValues values)
    {
        if (values.OmittedDisclosures.Contains(VsmeDisclosure.B1))
        {
            throw new InvalidVsmeDataException("B1 est la base du rapport : elle ne peut pas être omise.");
        }

        Short(values.LegalForm, "La forme juridique");
        Short(values.PrimaryCountry, "Le pays principal d'activité");
        Long(values.PracticesDescription, "La description des pratiques");
        Long(values.CircularEconomyDescription, "La description des principes d'économie circulaire");
        Long(values.MaterialFlowsDescription, "La description des flux de matières");
        Short(values.PollutionReportUrl, "Le lien vers la déclaration de pollution");

        if (values.PollutionReportUrl is { Length: > 0 } url
            && !(Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"))
        {
            throw new InvalidVsmeDataException("Le lien vers la déclaration de pollution doit être une adresse web (https://…).");
        }

        foreach (var list in new[] { values.Subsidiaries.Count, values.Certifications.Count, values.Pollutants.Count, values.EmployeesByCountry.Count })
        {
            if (list > MaxListItems)
            {
                throw new InvalidVsmeDataException($"Une liste ne peut pas dépasser {MaxListItems} éléments.");
            }
        }

        foreach (var subsidiary in values.Subsidiaries)
        {
            Required(subsidiary.Name, "Le nom d'une filiale");
            Required(subsidiary.RegisteredAddress, "L'adresse du siège d'une filiale");
            Short(subsidiary.RegisteredAddress, "L'adresse du siège d'une filiale");
        }

        foreach (var certification in values.Certifications)
        {
            Required(certification.Name, "Le nom d'un label");
            Short(certification.Issuer, "L'organisme d'un label");
            Short(certification.Rating, "La note d'un label");
        }

        foreach (var pollutant in values.Pollutants)
        {
            Required(pollutant.Name, "Le nom d'un polluant");
            Required(pollutant.Unit, "L'unité d'un polluant");
            NonNegative(pollutant.Quantity, "La quantité d'un polluant");
        }

        foreach (var country in values.EmployeesByCountry)
        {
            Required(country.Country, "Le pays d'une ligne d'effectif");
            NonNegative(country.Employees, "L'effectif d'un pays");
        }

        NonNegative(values.TotalAssetsEur, "Le total du bilan");
        NonNegative(values.CorruptionConvictions, "Le nombre de condamnations");
        NonNegative(values.CorruptionFinesEur, "Le montant des amendes");
    }

    private static void Validate(CompanySiteDetails details)
    {
        Required(details.Name, "Le nom du site");
        Required(details.Address, "L'adresse du site");
        if (details.Name.Trim().Length > CompanySite.NameMaxLength)
        {
            throw new InvalidVsmeDataException($"Le nom du site ne doit pas dépasser {CompanySite.NameMaxLength} caractères.");
        }

        if (details.Address.Trim().Length > CompanySite.AddressMaxLength)
        {
            throw new InvalidVsmeDataException($"L'adresse du site ne doit pas dépasser {CompanySite.AddressMaxLength} caractères.");
        }

        if (details.SensitiveAreaName is { } area && area.Trim().Length > CompanySite.SensitiveAreaNameMaxLength)
        {
            throw new InvalidVsmeDataException($"Le nom de la zone sensible ne doit pas dépasser {CompanySite.SensitiveAreaNameMaxLength} caractères.");
        }
    }

    private static void Required(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidVsmeDataException($"{label} est obligatoire.");
        }

        Short(value, label);
    }

    private static void Short(string? value, string label)
    {
        if (value is not null && value.Trim().Length > MaxShortTextLength)
        {
            throw new InvalidVsmeDataException($"{label} ne doit pas dépasser {MaxShortTextLength} caractères.");
        }
    }

    private static void Long(string? value, string label)
    {
        if (value is not null && value.Trim().Length > MaxTextLength)
        {
            throw new InvalidVsmeDataException($"{label} ne doit pas dépasser {MaxTextLength} caractères.");
        }
    }

    private static void NonNegative(double? value, string label)
    {
        if (value is < 0 || (value is { } v && (double.IsNaN(v) || double.IsInfinity(v))))
        {
            throw new InvalidVsmeDataException($"{label} ne peut pas être négatif.");
        }
    }
}
