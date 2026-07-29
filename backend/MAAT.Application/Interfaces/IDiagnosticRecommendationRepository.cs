using MAAT.Application.DTOs;
using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// DiagnosticRecommendation n'a pas de company_id direct : le filtrage passe par une
// jointure vers Diagnostic (voir IDiagnosticRepository pour le principe général).
public interface IDiagnosticRecommendationRepository
{
    // Export RGPD (section 6, cas 18) : is_completed et priority_rank sont saisis par
    // l'utilisateur (cases à cocher du tableau de bord), pas dérivés — leur omission
    // rendrait l'export incomplet au regard des art. 15 et 20.
    Task<IReadOnlyList<DiagnosticRecommendation>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // Complétion (recommandations.md, section 1) : diagnosticId doit avoir été validé au
    // préalable, comme pour IResponseRepository.AddAsync / IDomainScoreRepository.AddAsync.
    Task AddRangeAsync(IReadOnlyList<DiagnosticRecommendation> recommendations, CancellationToken ct);

    // Consultation (section 4) : la jointure vers Recommendation ignore délibérément
    // is_active — une recommandation désactivée après coup reste visible dans un plan
    // d'actions déjà émis (cas 14). Triée par priority_rank (cas 13).
    Task<IReadOnlyList<DiagnosticRecommendationView>> FindAllForDiagnosticAsync(Guid diagnosticId, CancellationToken ct);

    // Suivi (section 5) : la ligne exacte à basculer, résolue depuis (diagnosticId,
    // recommendationId) après résolution du code d'URL par IRecommendationRepository.
    Task<DiagnosticRecommendation?> FindAsync(Guid diagnosticId, Guid recommendationId, CancellationToken ct);
}
