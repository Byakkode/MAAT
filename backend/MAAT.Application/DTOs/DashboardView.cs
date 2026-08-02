using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/dashboard.md, section 1 (GET /api/dashboard) : un seul appel retourne tout
// l'écran. Les trois états de la section 1 se lisent sur HasCompletedDiagnostic et
// InProgressDiagnostic : aucun diagnostic (les deux sont respectivement false/null et null),
// diagnostic en cours seul (HasCompletedDiagnostic=false, InProgressDiagnostic renseigné), au
// moins un complété (contenu complet, InProgressDiagnostic renseigné ou non selon qu'un
// nouveau diagnostic est en cours en parallèle du dernier complété).
public sealed record DashboardView(
    bool HasCompletedDiagnostic,
    LatestDiagnosticView? LatestDiagnostic,
    IReadOnlyList<DomainScoreView> DomainScores,
    IReadOnlyList<DiagnosticHistoryPoint> History,
    SectorBenchmarkView? Benchmark,
    ActionPlanView ActionPlan,
    InProgressDiagnosticView? InProgressDiagnostic);

// SectorCode accompagne le score (section 2) : c'est ce qui rend le chiffre explicable.
public sealed record LatestDiagnosticView(Guid Id, decimal GlobalScore, DateTimeOffset CompletedAt, string SectorCode);

// TriggeredRecommendationCount porte sur l'intégralité du plan d'actions du diagnostic, pas
// seulement sur ActionPlanView.Items (au plus cinq) : le survol du radar (section 3) doit
// annoncer le nombre réel de recommandations déclenchées par ce domaine, y compris celles qui
// n'entrent pas dans les cinq premières affichées.
public sealed record DomainScoreView(RseDomain Domain, decimal Score, decimal SectorWeight, int TriggeredRecommendationCount);

// DeltaFromPrevious est null pour le premier point de l'historique (rien à comparer) — jamais
// pour les suivants, où il porte l'écart affiché en section 4 (« +7 points depuis février »).
public sealed record DiagnosticHistoryPoint(DateTimeOffset CompletedAt, decimal GlobalScore, decimal? DeltaFromPrevious);

// Available=false : Reason motive le seuil non atteint, Percentile est null. Available=true :
// Percentile est renseigné, Reason est null (docs/specs/dashboard.md, section 5, cas 8/9).
public sealed record SectorBenchmarkView(bool Available, int SampleSize, int? Percentile, string? Reason);

// TotalCount et CompletedCount portent sur l'intégralité du plan, pas seulement sur Items (au
// plus cinq, section 6) : « 3 actions terminées sur 24 » doit rester vrai même si les trois
// terminées ne sont pas parmi les cinq affichées.
public sealed record ActionPlanView(IReadOnlyList<DiagnosticRecommendationView> Items, int TotalCount, int CompletedCount);

public sealed record InProgressDiagnosticView(Guid Id, int AnsweredCount, int TotalActiveQuestions);
