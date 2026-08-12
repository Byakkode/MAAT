using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/recommandations.md, section 3.
public class RecommendationCalibrationCheckerTests
{
    // 11 questions de poids 1.00 dans le même domaine (Σ5w = 55), même structure que le
    // domaine Environmental réel. La borne pour trigger_max_value=3, poids 1 est
    // (5-3)×1/55×100 = 3,6363…, qui ne tombe pas juste — arrondie par quiconque rédige le
    // contenu (numeric(4,2), modele-donnees.md) à 3,64.
    private static List<Question> ElevenWeightOneQuestions() =>
        Enumerable.Range(1, 11)
            .Select(i => new Question($"ENV-{i:00}", $"Question {i}", RseDomain.Environmental, 1.00m, i))
            .ToList();

    [Fact]
    public void Ne_signale_pas_un_ecart_du_uniquement_a_l_arrondi_de_stockage_de_la_borne()
    {
        var questions = ElevenWeightOneQuestions();

        // impact_points = 3.64, exactement la borne théorique (3,6363…) arrondie à la
        // précision de stockage — pas un dépassement, une valeur correctement calibrée.
        // Une comparaison stricte à la borne en pleine précision (3,6363636…) la signalerait
        // à tort : c'est le bug que ce test verrouille.
        var recommendation = new Recommendation(
            "REC-ENV-01", RseDomain.Environmental, "Action test.", impactPoints: 3.64m,
            EffortLevel.Low, triggerQuestionCode: "ENV-01", triggerMaxValue: 3);

        var warnings = RecommendationCalibrationChecker.FindMiscalibrated([recommendation], questions);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Signale_toujours_un_ecart_reel_au_dela_de_l_arrondi()
    {
        var questions = ElevenWeightOneQuestions();

        // 3.70 dépasse la borne arrondie (3.64) de six centimes, pas d'un artefact d'arrondi :
        // doit rester signalé après la correction.
        var recommendation = new Recommendation(
            "REC-ENV-01", RseDomain.Environmental, "Action test.", impactPoints: 3.70m,
            EffortLevel.Low, triggerQuestionCode: "ENV-01", triggerMaxValue: 3);

        var warning = Assert.Single(RecommendationCalibrationChecker.FindMiscalibrated([recommendation], questions));

        Assert.Equal("REC-ENV-01", warning.RecommendationCode);
        Assert.Equal(3.70m, warning.ImpactPoints);
    }
}
