using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Domain.Knowledge;

namespace MAAT.Application.UseCases;

// docs/specs/documentation.md. Le sommaire et la recherche sont ouverts à toutes les offres :
// un compte Starter voit ce que la base contient. Lire un article demande l'offre Essential
// (abonnement.md, section 8), et le serveur l'applique — l'écran ne fait que le refléter.
public class DocumentationService(IKnowledgeBase knowledgeBase, CurrentPlanService currentPlan)
{
    public IReadOnlyList<KnowledgeArticle> Search(string? query, KnowledgeCategory? category, KnowledgeLevel? level) =>
        KnowledgeSearch.Search(knowledgeBase.Articles, query, category, level);

    // Nombre d'articles par rubrique, sur toute la base : le compteur d'une rubrique ne change
    // pas selon la recherche en cours.
    public IReadOnlyDictionary<KnowledgeCategory, int> CountByCategory() =>
        knowledgeBase.Articles.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count());

    // null : article inconnu (404), vérifié avant le droit — une adresse erronée n'a pas à
    // proposer un changement d'offre.
    public async Task<KnowledgeArticle?> GetAsync(string slug, CancellationToken ct)
    {
        var article = knowledgeBase.Articles.FirstOrDefault(a => a.Slug == slug);
        if (article is null)
        {
            return null;
        }

        await currentPlan.EnsureAsync(e => e.CanReadDocumentation, SubscriptionPlan.Essential, ct);
        return article;
    }
}
