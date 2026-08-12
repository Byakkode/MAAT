using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/recommandations.md, section 6 et cas 22-23. Contre la base réelle (migrations
// + HasData de QuestionConfiguration et RecommendationConfiguration), pas un fixture dédié
// à ce test — même principe que SectorWeightSeedTests, avec lequel cette classe partage
// PostgresFixture.
[Collection("Postgres")]
public class RecommendationSeedTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Cas22_Chaque_question_active_est_referencee_par_au_moins_une_recommandation_active()
    {
        await using var context = fixture.CreateContext();

        var activeQuestionCodes = await context.Questions.AsNoTracking()
            .Where(q => q.IsActive)
            .Select(q => q.Code)
            .ToListAsync();

        var referencedCodes = await context.Recommendations.AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => r.TriggerQuestionCode)
            .Distinct()
            .ToListAsync();

        var uncovered = activeQuestionCodes.Except(referencedCodes).ToList();

        Assert.True(
            uncovered.Count == 0,
            $"Questions actives sans recommandation active associée : {string.Join(", ", uncovered)}");
    }

    [Fact]
    public async Task Cas23_Le_controle_de_calibration_ne_signale_aucune_recommandation_du_seed_mais_detecte_une_recommandation_fabriquee_hors_borne()
    {
        await using var context = fixture.CreateContext();

        var activeQuestions = await context.Questions.AsNoTracking().Where(q => q.IsActive).ToListAsync();
        var seededRecommendations = await context.Recommendations.AsNoTracking().Where(r => r.IsActive).ToListAsync();

        var seedWarnings = RecommendationCalibrationChecker.FindMiscalibrated(seededRecommendations, activeQuestions);
        Assert.Empty(seedWarnings);

        // Recommandation fabriquée, jamais persistée : impact_points (99) très supérieur au
        // gain maximal théorique possible (borné à 100) quelle que soit la question
        // déclencheuse — délibérément hors borne. La borne attendue se recalcule ici à partir
        // du poids réel d'ENV-01 et de la somme Σ5w de son domaine, plutôt que d'être une
        // valeur figée : les deux dérivent du contenu réel de questions.csv
        // (docs/specs/referentiel.md), qui évolue.
        var triggerQuestion = activeQuestions.Single(q => q.Code == "ENV-01");
        var domainWeightSum = activeQuestions.Where(q => q.Domain == RseDomain.Environmental).Sum(q => q.Weight * 5m);
        var expectedMaxGain = (5 - 2) * triggerQuestion.Weight / domainWeightSum * 100m;

        var miscalibrated = new Recommendation(
            "REC-TEST-MISCALIBRATED", RseDomain.Environmental, "Recommandation de test mal calibrée.",
            impactPoints: 99.00m, EffortLevel.Low, triggerQuestionCode: "ENV-01", triggerMaxValue: 2);

        var withFabricated = RecommendationCalibrationChecker.FindMiscalibrated(
            [.. seededRecommendations, miscalibrated], activeQuestions);

        var warning = Assert.Single(withFabricated);
        Assert.Equal("REC-TEST-MISCALIBRATED", warning.RecommendationCode);
        Assert.Equal(99.00m, warning.ImpactPoints);
        Assert.Equal(expectedMaxGain, warning.MaxTheoreticalGain);
    }
}
