using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/recommandations.md, section 4 (GET /api/diagnostics/{id}/recommendations) :
// une recommandation déclenchée pour ce diagnostic, triée par priority_rank. La jointure
// vers Recommendation ignore is_active — voir IDiagnosticRecommendationRepository.
public sealed record DiagnosticRecommendationView(
    string Code,
    string ActionText,
    string? DetailText,
    RseDomain Domain,
    EffortLevel EffortLevel,
    decimal ImpactPoints,
    int PriorityRank,
    bool IsCompleted,
    DateTimeOffset? CompletedAt);

// docs/specs/abonnement.md, section 8 : les recommandations que l'offre laisse voir (les N
// premières par priority_rank) et le nombre total déclenché pour ce diagnostic.
public sealed record VisibleRecommendations(IReadOnlyList<DiagnosticRecommendationView> Items, int TotalCount);
