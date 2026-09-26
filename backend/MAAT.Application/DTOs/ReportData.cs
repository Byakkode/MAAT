using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/rapport-pdf.md, section 4. Tout ce dont IReportGenerator a besoin pour composer
// le document, assemblé par DiagnosticService.GenerateReportAsync depuis des données déjà
// persistées (aucune I/O, aucune horloge système côté générateur — section 3, déterminisme).
// GeneratedAt est une valeur transmise par l'appelant (TimeProvider injecté), jamais lue par
// le générateur lui-même.
// DefaultSectorWeightingApplied vient de Diagnostic.DefaultSectorWeightingApplied, décidé une
// fois à la complétion (questionnaire.md, section 6, cas 13) — jamais dérivé de
// ReportDomainScore.SectorWeight ci-dessous : la renormalisation de scoring.md (cas 7) ramène
// le coefficient effectif à 1.00 quand un seul domaine est actif, que la pondération d'origine
// soit spécifique ou par défaut, ce qui rendrait les deux cas indiscernables à cette étape.
public sealed record ReportData(
    string CompanyName,
    string SectorCode,
    CompanySizeRange SizeRange,
    string Region,
    DateTimeOffset CompletedAt,
    decimal GlobalScore,
    string GlobalScoreLabel,
    bool DefaultSectorWeightingApplied,
    IReadOnlyList<ReportDomainScore> DomainScores,
    IReadOnlyList<ReportRecommendation> Recommendations,
    int TotalRecommendationCount,
    ReportActionStatusSummary ActionStatusSummary,
    IReadOnlyList<ReportHistoryPoint> History,
    IReadOnlyDictionary<RseDomain, decimal>? PreviousDomainScores,
    ReportIndicators? Indicators,
    DateTimeOffset GeneratedAt,
    string ReferentialVersion);

// Numerator/Denominator : Σ(r×w) et Σ(w×5) du domaine, persistés dans DomainScore
// (modele-donnees.md) — jamais recalculés à la génération, pour la raison qui y est
// documentée. Contribution = Score × SectorWeight, affichée telle quelle dans le tableau de
// détail (section 4) : part du domaine dans le score global.
public sealed record ReportDomainScore(
    RseDomain Domain,
    decimal Score,
    decimal SectorWeight,
    decimal Numerator,
    decimal Denominator)
{
    public decimal Contribution => Score * SectorWeight;
}

// Limité aux vingt premières par priority_rank côté appelant (section 4) : cette liste ne
// porte que ce qui doit apparaître dans le document, TotalRecommendationCount ci-dessus porte
// le total réel. Status, AssignedTo et DueDate viennent du suivi ActionItemProgress
// (recommandations.md, section 5) ; Status vaut Done dès que IsCompleted est vrai, même sans
// ligne de suivi (case cochée depuis le tableau de bord), et Planned par défaut.
public sealed record ReportRecommendation(
    int PriorityRank,
    string ActionText,
    string? DetailText,
    RseDomain Domain,
    EffortLevel EffortLevel,
    decimal ImpactPoints,
    ActionItemStatus Status,
    string? AssignedTo,
    DateTimeOffset? DueDate)
{
    public bool IsCompleted => Status == ActionItemStatus.Done;
}

// Répartition des statuts sur l'intégralité du plan d'actions, pas seulement sur les vingt
// actions affichées : « 3 terminées sur 35 » doit rester vrai même si les trois terminées
// sont au-delà de la vingtième (même raison que ActionPlanView côté tableau de bord).
public sealed record ReportActionStatusSummary(int Planned, int InProgress, int Blocked, int Done)
{
    public int Total => Planned + InProgress + Blocked + Done;
}

// Diagnostics complétés de l'entreprise jusqu'à celui du rapport inclus, du plus ancien au
// plus récent — jamais un diagnostic postérieur : un rapport régénéré plus tard doit raconter
// la même histoire qu'au jour de sa complétion (section 3).
public sealed record ReportHistoryPoint(DateTimeOffset CompletedAt, decimal GlobalScore);

// Indicateurs quantitatifs saisis par l'entreprise (écran Indicateurs), pour l'année de
// référence du rapport et, si elle existe, l'année précédente pour la tendance.
public sealed record ReportIndicators(int Year, int? PreviousYear, IReadOnlyList<ReportIndicator> Items);

// Group/Label/Unit : mêmes libellés que frontend/src/pages/IndicatorsPage.tsx.
// HigherIsBetter : sens de lecture de la tendance (null quand la valeur n'a pas de bon sens
// intrinsèque, ex. le chiffre d'affaires ou l'effectif).
public sealed record ReportIndicator(
    string Group,
    string Label,
    string Unit,
    double? Value,
    double? PreviousValue,
    bool? HigherIsBetter);

// Retour de DiagnosticService.GenerateReportAsync : le document et le nom de fichier
// déterministe attendu par le Content-Disposition (rapport-pdf.md, section 2).
public sealed record GeneratedReport(byte[] Bytes, string FileName);
