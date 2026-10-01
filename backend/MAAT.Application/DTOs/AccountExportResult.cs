using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/auth-securite-rgpd.md, section 6 (POST /api/me/export) : export de
// l'intégralité des données du compte et de son entreprise. PasswordHash n'apparaît
// jamais ici, conformément à docs/specs/modele-donnees.md ("Ne jamais exposer
// password_hash dans un DTO, sous aucun prétexte").
public sealed record AccountExportResult(
    UserExport User,
    CompanyExport Company,
    IReadOnlyList<DiagnosticExport> Diagnostics,
    IReadOnlyList<ResponseExport> Responses,
    IReadOnlyList<DomainScoreExport> DomainScores,
    IReadOnlyList<DiagnosticRecommendationExport> DiagnosticRecommendations,
    IReadOnlyList<ReportExport> Reports,
    SubscriptionExport? Subscription,
    IReadOnlyList<RseIndicatorsExport> RseIndicators,
    IReadOnlyList<VsmeStatementExport> VsmeStatements,
    IReadOnlyList<CompanySiteExport> CompanySites,
    IReadOnlyList<ActionItemProgressExport> ActionItemProgress,
    IReadOnlyList<ActionItemChangeExport> ActionItemChanges,
    IReadOnlyList<SupportTicketExport> SupportTickets,
    CompanyLogoExport? CompanyLogo);

public sealed record UserExport(
    Guid Id,
    string Email,
    UserRole Role,
    bool EmailVerified,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset? LastLogin,
    DateTimeOffset CreatedAt);

public sealed record CompanyExport(
    Guid Id,
    string Name,
    string SectorCode,
    CompanySizeRange SizeRange,
    string Region,
    string? Siret,
    DateTimeOffset CreatedAt);

public sealed record DiagnosticExport(
    Guid Id,
    DiagnosticStatus Status,
    decimal? GlobalScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ResponseExport(
    Guid Id,
    Guid DiagnosticId,
    Guid QuestionId,
    int Value,
    DateTimeOffset AnsweredAt);

public sealed record DomainScoreExport(
    Guid DiagnosticId,
    RseDomain Domain,
    decimal Score,
    decimal SectorWeight);

// is_completed et completed_at sont saisis par l'utilisateur (cases à cocher du
// tableau de bord), pas dérivés du diagnostic — l'export doit les porter.
public sealed record DiagnosticRecommendationExport(
    Guid DiagnosticId,
    Guid RecommendationId,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    int PriorityRank);

public sealed record ReportExport(
    Guid Id,
    Guid DiagnosticId,
    ReportFormat Format,
    DateTimeOffset GeneratedAt,
    Guid GeneratedByUserId);

// Identifiants du prestataire de paiement exclus : ce sont des références techniques, pas
// des données de l'entreprise. Les factures se téléchargent depuis le portail client.
public sealed record SubscriptionExport(
    SubscriptionPlan Plan,
    BillingPeriod? BillingPeriod,
    SubscriptionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Indicateurs chiffrés d'un exercice, saisis par l'entreprise (écran Indicateurs). Propriétés
// nommées plutôt que record positionnel, pour la même raison que RseIndicatorValues : avec une
// quarantaine de double? consécutifs, un argument décalé passerait la compilation.
public sealed record RseIndicatorsExport
{
    public required Guid Id { get; init; }
    public required int Year { get; init; }

    public double? Co2EmissionsTons { get; init; }
    public double? EnergyConsumptionKwh { get; init; }
    public double? RenewableEnergyPct { get; init; }
    public double? WaterConsumptionM3 { get; init; }
    public double? WasteTons { get; init; }
    public double? RecyclingRatePct { get; init; }

    public double? ElectricityRenewableMwh { get; init; }
    public double? ElectricityNonRenewableMwh { get; init; }
    public double? FuelsRenewableMwh { get; init; }
    public double? FuelsNonRenewableMwh { get; init; }
    public double? Scope1Tco2e { get; init; }
    public double? Scope2LocationTco2e { get; init; }
    public double? WaterWithdrawalM3 { get; init; }
    public double? WaterConsumptionStressM3 { get; init; }
    public double? HazardousWasteTons { get; init; }
    public double? NonHazardousWasteTons { get; init; }

    public double? EmployeeCountFte { get; init; }
    public double? TurnoverRatePct { get; init; }
    public double? TrainingHoursPerEmployee { get; init; }
    public double? WorkAccidentRate { get; init; }
    public double? GenderEqualityIndex { get; init; }
    public double? PermanentContractPct { get; init; }

    public double? PermanentEmployees { get; init; }
    public double? TemporaryEmployees { get; init; }
    public double? FemaleEmployees { get; init; }
    public double? MaleEmployees { get; init; }
    public double? OtherGenderEmployees { get; init; }
    public int? RecordableAccidents { get; init; }
    public double? HoursWorked { get; init; }
    public int? WorkFatalities { get; init; }
    public double? GenderPayGapPct { get; init; }
    public double? CollectiveBargainingPct { get; init; }

    public double? LocalSuppliersPct { get; init; }
    public double? RseAssessedSuppliersPct { get; init; }
    public int? ActiveSuppliersCount { get; init; }
    public double? RevenueEur { get; init; }
    public double? RseInvestmentEur { get; init; }
    public double? ExportRevenuePct { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

// Déclarations d'un exercice pour la norme volontaire (docs/specs/norme-volontaire.md).
// Les listes stockées en JSON dans la ligne sont recopiées dans des records d'export : les
// types possédés mappés par EF ne sortent pas tels quels.
public sealed record VsmeStatementExport
{
    public required Guid Id { get; init; }
    public required int Year { get; init; }

    public ReportingBasis? ReportingBasis { get; init; }
    public string? LegalForm { get; init; }
    public double? TotalAssetsEur { get; init; }
    public string? PrimaryCountry { get; init; }
    public EmployeeCountUnit? EmployeeCountUnit { get; init; }
    public required IReadOnlyList<VsmeDisclosure> OmittedDisclosures { get; init; }
    public required IReadOnlyList<VsmeSubsidiaryExport> Subsidiaries { get; init; }
    public required IReadOnlyList<VsmeCertificationExport> Certifications { get; init; }

    public bool? HasPractices { get; init; }
    public bool? HasPolicies { get; init; }
    public bool? PoliciesPublic { get; init; }
    public bool? HasFutureInitiatives { get; init; }
    public bool? HasTargets { get; init; }
    public string? PracticesDescription { get; init; }
    public required IReadOnlyList<SustainabilityTopic> CoveredTopics { get; init; }

    public bool? PollutionReportingApplicable { get; init; }
    public string? PollutionReportUrl { get; init; }
    public required IReadOnlyList<VsmePollutantExport> Pollutants { get; init; }

    public bool? CircularEconomyApplied { get; init; }
    public string? CircularEconomyDescription { get; init; }
    public string? MaterialFlowsDescription { get; init; }

    public required IReadOnlyList<VsmeCountryHeadcountExport> EmployeesByCountry { get; init; }
    public bool? MinimumWageMet { get; init; }
    public int? CorruptionConvictions { get; init; }
    public double? CorruptionFinesEur { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record VsmeSubsidiaryExport(string Name, string RegisteredAddress);

public sealed record VsmeCertificationExport(string Name, string? Issuer, DateOnly? ObtainedOn, string? Rating);

public sealed record VsmePollutantExport(string Name, PollutionMedium Medium, double Quantity, string Unit);

public sealed record VsmeCountryHeadcountExport(string Country, double Employees);

// Valeurs enregistrées, pas les valeurs « effectives » de CompanySite : celles-ci se déduisent
// de la réponse de l'utilisateur et du résultat de la détection, tous deux exportés ici.
public sealed record CompanySiteExport(
    Guid Id,
    string Name,
    string Address,
    SiteTenure Tenure,
    double? Latitude,
    double? Longitude,
    string? GeocodedLabel,
    bool? InOrNearSensitiveArea,
    string? SensitiveAreaName,
    DateTimeOffset? SensitiveAreasCheckedAt,
    string? DetectedSensitiveAreas,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Suivi d'une action du plan d'actions : statut, responsable, échéance et notes sont saisis
// par l'entreprise (recommandations.md, section 4 bis).
public sealed record ActionItemProgressExport(
    Guid Id,
    Guid DiagnosticId,
    string RecommendationCode,
    ActionItemStatus Status,
    string? AssignedTo,
    DateTimeOffset? DueDate,
    string? Notes,
    DateTimeOffset UpdatedAt);

// ChangedByUserId plutôt que l'adresse de l'auteur : l'export d'un compte ne doit pas livrer
// les adresses des autres comptes de l'entreprise (art. 15 §4 et 20 §4 : droits des tiers).
public sealed record ActionItemChangeExport(
    Guid Id,
    Guid DiagnosticId,
    string RecommendationCode,
    ActionItemField Field,
    string? OldValue,
    string? NewValue,
    Guid? ChangedByUserId,
    DateTimeOffset ChangedAt);

public sealed record SupportTicketExport(
    Guid Id,
    Guid UserId,
    int GithubIssueNumber,
    string GithubIssueUrl,
    string Title,
    string Description,
    string TicketType,
    DateTimeOffset CreatedAt);

// Le logo voyage dans le JSON, en base64 (sérialisation par défaut d'un byte[]), plutôt
// qu'un renvoi vers GET /api/company/logo : l'export reste un fichier unique, lisible sans
// session ouverte ni compte encore existant, ce que la portabilité (art. 20) suppose. Le
// coût est faible : l'image est normalisée à 600 px au plus, quelques dizaines de Ko.
public sealed record CompanyLogoExport(
    string ContentType,
    byte[] PngContent,
    DateTimeOffset UpdatedAt);
