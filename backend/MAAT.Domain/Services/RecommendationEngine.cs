using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

public sealed record PrioritizedRecommendation(Recommendation Recommendation, int PriorityRank);

// docs/specs/recommandations.md, section 1 : la complétion exige déjà que toutes les
// questions actives soient répondues, donc une question déclencheuse sans réponse ne peut
// normalement pas se produire à ce stade. Si le cas survient malgré tout, c'est une
// incohérence de données — l'appelant ne doit pas l'absorber silencieusement.
public sealed class MissingTriggerResponseException(string questionCode)
    : Exception($"Aucune réponse trouvée pour la question déclencheuse '{questionCode}'.")
{
    public string QuestionCode { get; } = questionCode;
}

// section 2 : un domaine de recommandation sans pondération sectorielle effective calculée
// (aucune question active dans ce domaine) — incohérence de données symétrique de
// SectorWeightNotFoundException côté ScoringService.
public sealed class RecommendationDomainWeightNotFoundException(RseDomain domain)
    : Exception($"Le domaine '{domain}' n'a aucune pondération effective calculée pour la priorisation des recommandations.")
{
    public RseDomain Domain { get; } = domain;
}

// Interface introduite pour la même raison que IScoringService (voir son commentaire) :
// permettre à DiagnosticService.CompleteAsync de recevoir une implémentation substituée en
// test (docs/specs/recommandations.md, cas 12 — échec de la sélection annule la complétion).
public interface IRecommendationEngine
{
    IReadOnlyList<Recommendation> SelectTriggered(
        IReadOnlyList<Recommendation> activeRecommendations,
        IReadOnlyDictionary<string, int> responseValueByQuestionCode);

    IReadOnlyList<PrioritizedRecommendation> Prioritize(
        IReadOnlyList<Recommendation> triggered,
        IReadOnlyDictionary<RseDomain, decimal> effectiveSectorWeightByDomain);
}

public sealed class RecommendationEngine : IRecommendationEngine
{
    private static readonly IReadOnlyDictionary<EffortLevel, int> EffortCost = new Dictionary<EffortLevel, int>
    {
        [EffortLevel.Low] = 1,
        [EffortLevel.Medium] = 2,
        [EffortLevel.High] = 3,
    };

    // section 1 : Response.value ≤ Recommendation.trigger_max_value, comparaison inclusive
    // (cas 1 et 2). L'appelant ne doit fournir que des recommandations actives (cas 4) :
    // is_active n'est pas revérifié ici, exactement comme ScoringService ne revérifie pas
    // Question.is_active et fait confiance à la liste que lui passe l'appelant.
    public IReadOnlyList<Recommendation> SelectTriggered(
        IReadOnlyList<Recommendation> activeRecommendations,
        IReadOnlyDictionary<string, int> responseValueByQuestionCode)
    {
        var triggered = new List<Recommendation>();
        foreach (var recommendation in activeRecommendations)
        {
            if (!responseValueByQuestionCode.TryGetValue(recommendation.TriggerQuestionCode, out var value))
            {
                throw new MissingTriggerResponseException(recommendation.TriggerQuestionCode);
            }

            if (value <= recommendation.TriggerMaxValue)
            {
                triggered.Add(recommendation);
            }
        }

        return triggered;
    }

    // section 2 : priorité = (impact_points × ρ_domaine) / coût_effort, triée décroissante,
    // départage par code croissant (cas 8) — déterminisme requis par la régénération à
    // l'identique du rapport PDF (cas 9 et 11). ρ_domaine est celui effectivement persisté
    // dans DomainScore (cas 11) : l'appelant le fournit, ce service ne relit jamais
    // SectorWeight lui-même.
    public IReadOnlyList<PrioritizedRecommendation> Prioritize(
        IReadOnlyList<Recommendation> triggered,
        IReadOnlyDictionary<RseDomain, decimal> effectiveSectorWeightByDomain)
    {
        var withPriority = triggered
            .Select(r =>
            {
                if (!effectiveSectorWeightByDomain.TryGetValue(r.Domain, out var weight))
                {
                    throw new RecommendationDomainWeightNotFoundException(r.Domain);
                }

                var priority = r.ImpactPoints * weight / EffortCost[r.EffortLevel];
                return (Recommendation: r, Priority: priority);
            })
            .ToList();

        return withPriority
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Recommendation.Code, StringComparer.Ordinal)
            .Select((x, index) => new PrioritizedRecommendation(x.Recommendation, index + 1))
            .ToList();
    }
}
