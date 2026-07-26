// Signature publique de ScoringService déduite des tests ci-dessous.
// La classe n'existe pas encore : ce fichier ne compile pas, c'est attendu.
//
// namespace MAAT.Domain.Services;
//
// public sealed record QuestionScoreInput(
//     RseDomain Domain,
//     decimal Weight,
//     int? Value,
//     bool IsActive = true);
//
// public sealed record DomainScoreDetail(
//     RseDomain Domain,
//     decimal Numerator,          // Σ(rᵢ × wᵢ)
//     decimal Denominator,        // Σ(wᵢ × 5)
//     decimal Score,              // pleine précision, non arrondi
//     decimal EffectiveSectorWeight); // ρ_d après renormalisation éventuelle (cas 7)
//
// public sealed record ScoringResult(
//     decimal GlobalScore,        // pleine précision, non arrondi
//     IReadOnlyList<DomainScoreDetail> DomainScores);
//
// public sealed class ScoringService
// {
//     // Lève ArgumentOutOfRangeException si un r ∉ [0, 5] ou un w ≤ 0 (cas 9).
//     // Lève IncompleteDiagnosticException si une question active n'a pas de réponse (cas 8).
//     // Un domaine sans aucune question active est exclu et les ρ restants sont
//     // renormalisés pour que leur somme revienne à 1 (cas 7).
//     // Lève SectorWeightNotFoundException si un domaine a des questions actives
//     // mais aucune entrée dans sectorWeights (cas 11) — erreur de données de
//     // référence, distincte du cas 7 : ici le domaine a un score à agréger, pas
//     // de pondération pour le faire, donc jamais de repli silencieux à 0.
//     public ScoringResult CalculateScore(
//         IReadOnlyList<QuestionScoreInput> questions,
//         IReadOnlyDictionary<RseDomain, decimal> sectorWeights);
//
//     // Arrondi d'affichage isolé, MidpointRounding.AwayFromZero (cas 5).
//     public static int RoundForDisplay(decimal score);
// }
//
// public sealed class IncompleteDiagnosticException : Exception;
// public sealed class SectorWeightNotFoundException : Exception;

using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

public class ScoringServiceTests
{
    private static IReadOnlyDictionary<RseDomain, decimal> UniformSectorWeights() => new Dictionary<RseDomain, decimal>
    {
        [RseDomain.Environmental] = 0.20m,
        [RseDomain.Social] = 0.20m,
        [RseDomain.Ethics] = 0.20m,
        [RseDomain.Procurement] = 0.20m,
        [RseDomain.Governance] = 0.20m,
    };

    [Fact]
    public void Cas1_ToutesReponsesAZero_ScoreDomaineEtGlobalSontNuls()
    {
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 3m, Value: 0),
            new(RseDomain.Environmental, Weight: 2m, Value: 0),
            new(RseDomain.Social, Weight: 1m, Value: 0),
            new(RseDomain.Ethics, Weight: 4m, Value: 0),
            new(RseDomain.Procurement, Weight: 2m, Value: 0),
            new(RseDomain.Governance, Weight: 5m, Value: 0),
        };
        var sectorWeights = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Environmental] = 0.30m,
            [RseDomain.Social] = 0.25m,
            [RseDomain.Ethics] = 0.15m,
            [RseDomain.Procurement] = 0.10m,
            [RseDomain.Governance] = 0.20m,
        };

        var result = new ScoringService().CalculateScore(questions, sectorWeights);

        Assert.All(result.DomainScores, d => Assert.Equal(0m, d.Score));
        Assert.Equal(0m, result.GlobalScore);
    }

    [Fact]
    public void Cas2_ToutesReponsesAuMaximum_ScoreDomaineEtGlobalValent100()
    {
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 3m, Value: 5),
            new(RseDomain.Environmental, Weight: 2m, Value: 5),
            new(RseDomain.Social, Weight: 1m, Value: 5),
            new(RseDomain.Ethics, Weight: 4m, Value: 5),
            new(RseDomain.Procurement, Weight: 2m, Value: 5),
            new(RseDomain.Governance, Weight: 5m, Value: 5),
        };
        var sectorWeights = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Environmental] = 0.30m,
            [RseDomain.Social] = 0.25m,
            [RseDomain.Ethics] = 0.15m,
            [RseDomain.Procurement] = 0.10m,
            [RseDomain.Governance] = 0.20m,
        };

        var result = new ScoringService().CalculateScore(questions, sectorWeights);

        // Σρ_d = 1 : si ce n'était pas le cas, le score global s'écarterait de 100.
        Assert.All(result.DomainScores, d => Assert.Equal(100m, d.Score));
        Assert.Equal(100m, result.GlobalScore);
    }

    [Fact]
    public void Cas3_ScoreDomaine_CalculDetaille_Environnement()
    {
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 3m, Value: 5), // ENV-01 : 3 × 5 = 15
            new(RseDomain.Environmental, Weight: 2m, Value: 2), // ENV-02 : 2 × 2 = 4
            new(RseDomain.Environmental, Weight: 1m, Value: 0), // ENV-03 : 1 × 0 = 0
        };
        var sectorWeights = new Dictionary<RseDomain, decimal> { [RseDomain.Environmental] = 1m };

        var result = new ScoringService().CalculateScore(questions, sectorWeights);

        var env = Assert.Single(result.DomainScores);
        Assert.Equal(RseDomain.Environmental, env.Domain);
        Assert.Equal(19m, env.Numerator);
        Assert.Equal(30m, env.Denominator);
        Assert.Equal(63.33m, Math.Round(env.Score, 2));
        Assert.Equal(63.33m, Math.Round(result.GlobalScore, 2));
    }

    [Fact]
    public void Cas4_PonderationSectorielle_MemesReponses_SecteursDifferents()
    {
        // Mêmes réponses pour les deux entreprises : scores de domaine identiques
        // (Environnement 63,3333… · Social 80 · Éthique 50 · Achats 40 · Gouvernance 25).
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 3m, Value: 5),
            new(RseDomain.Environmental, Weight: 2m, Value: 2),
            new(RseDomain.Environmental, Weight: 1m, Value: 0),
            new(RseDomain.Social, Weight: 1m, Value: 4),
            new(RseDomain.Ethics, Weight: 1m, Value: 2),
            new(RseDomain.Ethics, Weight: 1m, Value: 3),
            new(RseDomain.Procurement, Weight: 1m, Value: 2),
            new(RseDomain.Governance, Weight: 3m, Value: 1),
            new(RseDomain.Governance, Weight: 1m, Value: 2),
        };

        var transport = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Environmental] = 0.40m,
            [RseDomain.Social] = 0.20m,
            [RseDomain.Ethics] = 0.15m,
            [RseDomain.Procurement] = 0.15m,
            [RseDomain.Governance] = 0.10m,
        };
        var conseil = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Environmental] = 0.10m,
            [RseDomain.Social] = 0.30m,
            [RseDomain.Ethics] = 0.25m,
            [RseDomain.Procurement] = 0.15m,
            [RseDomain.Governance] = 0.20m,
        };

        var service = new ScoringService();

        var resultTransport = service.CalculateScore(questions, transport);
        var resultConseil = service.CalculateScore(questions, conseil);

        Assert.Equal(57.33m, Math.Round(resultTransport.GlobalScore, 2));
        Assert.Equal(53.83m, Math.Round(resultConseil.GlobalScore, 2));
    }

    [Fact]
    public void Cas5_ArrondiAffichage_MidpointRounding()
    {
        Assert.Equal(58, ScoringService.RoundForDisplay(57.50m));
        Assert.Equal(59, ScoringService.RoundForDisplay(58.50m));
        Assert.Equal(57, ScoringService.RoundForDisplay(57.49m));
        Assert.Equal(1, ScoringService.RoundForDisplay(0.50m));
    }

    [Fact]
    public void Cas6_SecteurNonCouvert_ReplisSurPonderationParDefaut()
    {
        // Mêmes réponses que le cas 4, secteur inconnu → pondération par défaut (0,20 chacun).
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 3m, Value: 5),
            new(RseDomain.Environmental, Weight: 2m, Value: 2),
            new(RseDomain.Environmental, Weight: 1m, Value: 0),
            new(RseDomain.Social, Weight: 1m, Value: 4),
            new(RseDomain.Ethics, Weight: 1m, Value: 2),
            new(RseDomain.Ethics, Weight: 1m, Value: 3),
            new(RseDomain.Procurement, Weight: 1m, Value: 2),
            new(RseDomain.Governance, Weight: 3m, Value: 1),
            new(RseDomain.Governance, Weight: 1m, Value: 2),
        };

        var result = new ScoringService().CalculateScore(questions, UniformSectorWeights());

        Assert.Equal(51.67m, Math.Round(result.GlobalScore, 2));
    }

    [Fact]
    public void Cas7_DomaineSansQuestionActive_ExcluEtRenormalise()
    {
        // Achats (ρ = 0,15) n'a aucune question active : exclu, les quatre coefficients
        // restants (0,30 · 0,30 · 0,20 · 0,05) sont divisés par 0,85.
        // (80×0,30 + 60×0,30 + 40×0,20 + 20×0,05) / 0,85 = 51 / 0,85 = 60.
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 1m, Value: 4), // 4/5 × 100 = 80
            new(RseDomain.Social, Weight: 1m, Value: 3),        // 3/5 × 100 = 60
            new(RseDomain.Ethics, Weight: 1m, Value: 2),        // 2/5 × 100 = 40
            new(RseDomain.Governance, Weight: 1m, Value: 1),    // 1/5 × 100 = 20
            new(RseDomain.Procurement, Weight: 1m, Value: null, IsActive: false),
        };
        var sectorWeights = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Environmental] = 0.30m,
            [RseDomain.Social] = 0.30m,
            [RseDomain.Ethics] = 0.20m,
            [RseDomain.Procurement] = 0.15m,
            [RseDomain.Governance] = 0.05m,
        };

        var result = new ScoringService().CalculateScore(questions, sectorWeights);

        Assert.Equal(4, result.DomainScores.Count);
        Assert.DoesNotContain(result.DomainScores, d => d.Domain == RseDomain.Procurement);
        Assert.Equal(60.00m, Math.Round(result.GlobalScore, 2));
    }

    [Fact]
    public void Cas8_DiagnosticIncomplet_QuestionActiveSansReponse_LeveException()
    {
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 1m, Value: null, IsActive: true),
        };

        Assert.Throws<IncompleteDiagnosticException>(
            () => new ScoringService().CalculateScore(questions, UniformSectorWeights()));
    }

    [Fact]
    public void Cas9_ValeursHorsBornes_LeveArgumentOutOfRangeException()
    {
        var sectorWeights = new Dictionary<RseDomain, decimal> { [RseDomain.Environmental] = 1m };
        var service = new ScoringService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CalculateScore(
            new List<QuestionScoreInput> { new(RseDomain.Environmental, Weight: 1m, Value: -1) },
            sectorWeights));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CalculateScore(
            new List<QuestionScoreInput> { new(RseDomain.Environmental, Weight: 1m, Value: 6) },
            sectorWeights));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CalculateScore(
            new List<QuestionScoreInput> { new(RseDomain.Environmental, Weight: 0m, Value: 3) },
            sectorWeights));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CalculateScore(
            new List<QuestionScoreInput> { new(RseDomain.Environmental, Weight: -1m, Value: 3) },
            sectorWeights));
    }

    [Fact]
    public void Cas11_DomaineActifAbsentDuDictionnaireSectoriel_LeveException()
    {
        // Le domaine Environmental a une question active mais aucune entrée dans
        // sectorWeights (qui ne couvre que Social) : erreur de données de
        // référence, distincte du cas 7 où le domaine n'a aucune question active.
        var questions = new List<QuestionScoreInput>
        {
            new(RseDomain.Environmental, Weight: 1m, Value: 3),
        };
        var sectorWeights = new Dictionary<RseDomain, decimal>
        {
            [RseDomain.Social] = 1m,
        };

        var exception = Assert.Throws<SectorWeightNotFoundException>(
            () => new ScoringService().CalculateScore(questions, sectorWeights));

        Assert.Equal(RseDomain.Environmental, exception.Domain);
    }
}
