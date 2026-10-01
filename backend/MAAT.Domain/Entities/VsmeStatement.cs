using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

// Déclarations d'un exercice pour le module de base de la norme volontaire
// (docs/specs/norme-volontaire.md, section 2) : tout ce qui n'est pas un indicateur chiffré de
// RseIndicators ni un site de l'entreprise. Une ligne par entreprise et par exercice, comme
// RseIndicators : la forme juridique, le périmètre ou les labels peuvent changer d'une année
// sur l'autre, et le rapport d'un exercice doit rester celui de cet exercice.
public class VsmeStatement
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public int Year { get; private set; }

    // B1
    public ReportingBasis? ReportingBasis { get; private set; }
    public string? LegalForm { get; private set; }
    public double? TotalAssetsEur { get; private set; }
    public string? PrimaryCountry { get; private set; }
    public EmployeeCountUnit? EmployeeCountUnit { get; private set; }
    public List<VsmeDisclosure> OmittedDisclosures { get; private set; } = [];
    public List<VsmeSubsidiary> Subsidiaries { get; private set; } = [];
    public List<VsmeCertification> Certifications { get; private set; } = [];

    // B2 : null = pas encore répondu, distinct de « non ».
    public bool? HasPractices { get; private set; }
    public bool? HasPolicies { get; private set; }
    public bool? PoliciesPublic { get; private set; }
    public bool? HasFutureInitiatives { get; private set; }
    public bool? HasTargets { get; private set; }
    public string? PracticesDescription { get; private set; }
    public List<SustainabilityTopic> CoveredTopics { get; private set; } = [];

    // B4
    public bool? PollutionReportingApplicable { get; private set; }
    public string? PollutionReportUrl { get; private set; }
    public List<VsmePollutant> Pollutants { get; private set; } = [];

    // B7
    public bool? CircularEconomyApplied { get; private set; }
    public string? CircularEconomyDescription { get; private set; }
    public string? MaterialFlowsDescription { get; private set; }

    // B8 §40 c : seulement si l'entreprise emploie dans plusieurs pays.
    public List<VsmeCountryHeadcount> EmployeesByCountry { get; private set; } = [];

    // B10 §42 a
    public bool? MinimumWageMet { get; private set; }

    // B11 : null = aucune condamnation déclarée (§15, information « si applicable »).
    public int? CorruptionConvictions { get; private set; }
    public double? CorruptionFinesEur { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private VsmeStatement() { }

    public VsmeStatement(Guid companyId, int year, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        Year = year;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void Update(VsmeStatementValues values, DateTimeOffset now)
    {
        ReportingBasis = values.ReportingBasis;
        LegalForm = Clean(values.LegalForm);
        TotalAssetsEur = values.TotalAssetsEur;
        PrimaryCountry = Clean(values.PrimaryCountry);
        EmployeeCountUnit = values.EmployeeCountUnit;
        // Distinct et trié : la liste figure dans le rapport, qui doit être identique à données
        // identiques (rapport-pdf.md, section 3), quel que soit l'ordre des cases cochées.
        OmittedDisclosures = [.. values.OmittedDisclosures.Distinct().Order()];
        // Les filiales n'ont de sens que pour un rapport consolidé (§27 d).
        Subsidiaries = values.ReportingBasis == Enums.ReportingBasis.Consolidated ? [.. values.Subsidiaries] : [];
        Certifications = [.. values.Certifications];

        HasPractices = values.HasPractices;
        HasPolicies = values.HasPolicies;
        PoliciesPublic = values.HasPolicies == true ? values.PoliciesPublic : null;
        HasFutureInitiatives = values.HasFutureInitiatives;
        HasTargets = values.HasTargets;
        PracticesDescription = Clean(values.PracticesDescription);
        CoveredTopics = [.. values.CoveredTopics.Distinct().Order()];

        PollutionReportingApplicable = values.PollutionReportingApplicable;
        PollutionReportUrl = values.PollutionReportingApplicable == true ? Clean(values.PollutionReportUrl) : null;
        Pollutants = values.PollutionReportingApplicable == true ? [.. values.Pollutants] : [];

        CircularEconomyApplied = values.CircularEconomyApplied;
        CircularEconomyDescription = values.CircularEconomyApplied == true ? Clean(values.CircularEconomyDescription) : null;
        MaterialFlowsDescription = Clean(values.MaterialFlowsDescription);

        EmployeesByCountry = [.. values.EmployeesByCountry];
        MinimumWageMet = values.MinimumWageMet;
        CorruptionConvictions = values.CorruptionConvictions;
        CorruptionFinesEur = values.CorruptionFinesEur;

        UpdatedAt = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record VsmeStatementValues
{
    public ReportingBasis? ReportingBasis { get; init; }
    public string? LegalForm { get; init; }
    public double? TotalAssetsEur { get; init; }
    public string? PrimaryCountry { get; init; }
    public EmployeeCountUnit? EmployeeCountUnit { get; init; }
    public IReadOnlyList<VsmeDisclosure> OmittedDisclosures { get; init; } = [];
    public IReadOnlyList<VsmeSubsidiary> Subsidiaries { get; init; } = [];
    public IReadOnlyList<VsmeCertification> Certifications { get; init; } = [];

    public bool? HasPractices { get; init; }
    public bool? HasPolicies { get; init; }
    public bool? PoliciesPublic { get; init; }
    public bool? HasFutureInitiatives { get; init; }
    public bool? HasTargets { get; init; }
    public string? PracticesDescription { get; init; }
    public IReadOnlyList<SustainabilityTopic> CoveredTopics { get; init; } = [];

    public bool? PollutionReportingApplicable { get; init; }
    public string? PollutionReportUrl { get; init; }
    public IReadOnlyList<VsmePollutant> Pollutants { get; init; } = [];

    public bool? CircularEconomyApplied { get; init; }
    public string? CircularEconomyDescription { get; init; }
    public string? MaterialFlowsDescription { get; init; }

    public IReadOnlyList<VsmeCountryHeadcount> EmployeesByCountry { get; init; } = [];
    public bool? MinimumWageMet { get; init; }
    public int? CorruptionConvictions { get; init; }
    public double? CorruptionFinesEur { get; init; }
}

// Éléments de liste, stockés en JSON dans la ligne de VsmeStatement (modele-donnees.md) :
// ils n'existent qu'à travers elle et ne sont jamais interrogés seuls.
public sealed class VsmeSubsidiary
{
    public string Name { get; private set; } = default!;
    public string RegisteredAddress { get; private set; } = default!;

    private VsmeSubsidiary() { }

    public VsmeSubsidiary(string name, string registeredAddress)
    {
        Name = name.Trim();
        RegisteredAddress = registeredAddress.Trim();
    }
}

public sealed class VsmeCertification
{
    public string Name { get; private set; } = default!;
    public string? Issuer { get; private set; }
    public DateOnly? ObtainedOn { get; private set; }
    public string? Rating { get; private set; }

    private VsmeCertification() { }

    public VsmeCertification(string name, string? issuer, DateOnly? obtainedOn, string? rating)
    {
        Name = name.Trim();
        Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer.Trim();
        ObtainedOn = obtainedOn;
        Rating = string.IsNullOrWhiteSpace(rating) ? null : rating.Trim();
    }
}

public sealed class VsmePollutant
{
    public string Name { get; private set; } = default!;
    public PollutionMedium Medium { get; private set; }
    public double Quantity { get; private set; }
    public string Unit { get; private set; } = default!;

    private VsmePollutant() { }

    public VsmePollutant(string name, PollutionMedium medium, double quantity, string unit)
    {
        Name = name.Trim();
        Medium = medium;
        Quantity = quantity;
        Unit = unit.Trim();
    }
}

public sealed class VsmeCountryHeadcount
{
    public string Country { get; private set; } = default!;
    public double Employees { get; private set; }

    private VsmeCountryHeadcount() { }

    public VsmeCountryHeadcount(string country, double employees)
    {
        Country = country.Trim();
        Employees = employees;
    }
}
