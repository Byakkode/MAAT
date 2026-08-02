using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Table de référence, comme IQuestionRepository : pas de filtrage par company_id.
public interface IRecommendationRepository
{
    // Complétion (recommandations.md, section 1) : seules les recommandations actives
    // sont évaluées.
    Task<IReadOnlyList<Recommendation>> FindAllActiveAsync(CancellationToken ct);

    // Résolution du code d'URL (PATCH .../recommendations/{recommendationCode}, section 5)
    // et jointure de consultation (section 4) : ignore is_active dans les deux cas — une
    // recommandation désactivée après coup reste visible et modifiable dans un plan
    // d'actions déjà émis.
    Task<Recommendation?> FindByCodeAsync(string code, CancellationToken ct);
}
