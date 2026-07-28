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
}
