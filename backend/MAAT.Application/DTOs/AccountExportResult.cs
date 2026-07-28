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
    IReadOnlyList<ReportExport> Reports);

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
