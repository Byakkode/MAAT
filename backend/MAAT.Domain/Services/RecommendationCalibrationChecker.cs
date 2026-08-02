using MAAT.Domain.Entities;

namespace MAAT.Domain.Services;

public sealed record RecommendationCalibrationWarning(
    string RecommendationCode,
    decimal ImpactPoints,
    decimal MaxTheoreticalGain);

// docs/specs/recommandations.md, section 3 : contrôle de cohérence du seed, pas une
// exception — un impact_points mal calibré ne doit jamais bloquer quoi que ce soit en
// production, seulement apparaître dans une liste d'avertissements (cas 23).
public static class RecommendationCalibrationChecker
{
    // Gain maximal théorique atteignable via la question déclencheuse :
    // (5 − trigger_max_value) × w / Σ(5w du domaine) × 100. Σ(5w du domaine) est le même
    // dénominateur que celui utilisé par ScoringService pour le score du domaine.
    public static IReadOnlyList<RecommendationCalibrationWarning> FindMiscalibrated(
        IReadOnlyList<Recommendation> recommendations,
        IReadOnlyList<Question> activeQuestions)
    {
        var questionByCode = activeQuestions.ToDictionary(q => q.Code);
        var domainWeightSum = activeQuestions
            .GroupBy(q => q.Domain)
            .ToDictionary(g => g.Key, g => g.Sum(q => q.Weight * 5m));

        var warnings = new List<RecommendationCalibrationWarning>();
        foreach (var recommendation in recommendations)
        {
            // Question déclencheuse inactive ou introuvable : hors périmètre de ce
            // contrôle, qui porte sur la calibration vis-à-vis d'une question active.
            if (!questionByCode.TryGetValue(recommendation.TriggerQuestionCode, out var question))
            {
                continue;
            }

            var maxGain = (5 - recommendation.TriggerMaxValue) * question.Weight / domainWeightSum[question.Domain] * 100m;
            if (recommendation.ImpactPoints > maxGain)
            {
                warnings.Add(new RecommendationCalibrationWarning(recommendation.Code, recommendation.ImpactPoints, maxGain));
            }
        }

        return warnings;
    }
}
