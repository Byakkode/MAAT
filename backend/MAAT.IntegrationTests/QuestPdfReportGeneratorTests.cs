using MAAT.Application.DTOs;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, section 4. Tests purs sur les méthodes internal de
// QuestPdfReportGenerator — aucune base de données, comme RadarAxisLayoutTests.
public class QuestPdfReportGeneratorTests
{
    private static ReportDomainScore MakeScore(RseDomain domain, decimal score, decimal sectorWeight = 0.20m) =>
        new(domain, score, sectorWeight, Numerator: score, Denominator: 100m);

    [Fact]
    public void ComputeStrengthsAndWeaknesses_avec_cinq_domaines_est_disjoint()
    {
        var domainScores = new List<ReportDomainScore>
        {
            MakeScore(RseDomain.Environmental, 80),
            MakeScore(RseDomain.Social, 60),
            MakeScore(RseDomain.Ethics, 40),
            MakeScore(RseDomain.Procurement, 20),
            MakeScore(RseDomain.Governance, 0),
        };

        var (strengths, weaknesses) = QuestPdfReportGenerator.ComputeStrengthsAndWeaknesses(domainScores);

        Assert.Equal([RseDomain.Environmental, RseDomain.Social], strengths.Select(d => d.Domain));
        Assert.Equal([RseDomain.Governance, RseDomain.Procurement], weaknesses.Select(d => d.Domain));
        Assert.Empty(strengths.Select(d => d.Domain).Intersect(weaknesses.Select(d => d.Domain)));
    }

    [Fact]
    public void ComputeStrengthsAndWeaknesses_avec_quatre_domaines_est_disjoint()
    {
        var domainScores = new List<ReportDomainScore>
        {
            MakeScore(RseDomain.Environmental, 80),
            MakeScore(RseDomain.Social, 60),
            MakeScore(RseDomain.Ethics, 40),
            MakeScore(RseDomain.Procurement, 20),
        };

        var (strengths, weaknesses) = QuestPdfReportGenerator.ComputeStrengthsAndWeaknesses(domainScores);

        Assert.Equal(2, strengths.Count);
        Assert.Equal(2, weaknesses.Count);
        Assert.Empty(strengths.Select(d => d.Domain).Intersect(weaknesses.Select(d => d.Domain)));
    }

    // Le bug réel : avant le correctif, trois domaines produisaient strengths=[d0,d1] et
    // weaknesses=[d2,d1] — d1 apparaissait comme point fort ET comme axe d'amélioration.
    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(1)]
    [InlineData(0)]
    public void ComputeStrengthsAndWeaknesses_avec_moins_de_quatre_domaines_ne_produit_rien(int count)
    {
        var domainScores = new List<ReportDomainScore>
        {
            MakeScore(RseDomain.Environmental, 80),
            MakeScore(RseDomain.Social, 60),
            MakeScore(RseDomain.Ethics, 40),
        }.Take(count).ToList();

        var (strengths, weaknesses) = QuestPdfReportGenerator.ComputeStrengthsAndWeaknesses(domainScores);

        Assert.Empty(strengths);
        Assert.Empty(weaknesses);
    }

    [Fact]
    public void ComputeSectorWeightingLabel_indique_explicitement_la_ponderation_par_defaut()
    {
        var label = QuestPdfReportGenerator.ComputeSectorWeightingLabel(defaultSectorWeightingApplied: true);

        Assert.Contains("non répertorié", label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("par défaut", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeSectorWeightingLabel_indique_une_ponderation_specifique()
    {
        var label = QuestPdfReportGenerator.ComputeSectorWeightingLabel(defaultSectorWeightingApplied: false);

        Assert.DoesNotContain("non répertorié", label, StringComparison.OrdinalIgnoreCase);
    }

    // Le bug réel (corrigé) : inférer ce libellé depuis ReportDomainScore.SectorWeight se
    // trompait quand un seul domaine était actif — la renormalisation de scoring.md (cas 7)
    // ramène alors le coefficient effectif à 1.00 quel que soit le repli, rendant les deux cas
    // indiscernables. ComputeSectorWeightingLabel ne prend plus SectorWeight en entrée du
    // tout : ce test fige la signature pour qu'elle ne puisse pas y revenir silencieusement.
    [Fact]
    public void ComputeSectorWeightingLabel_ne_depend_pas_de_SectorWeight()
    {
        var singleDomainWithSectorWeightOne = new List<ReportDomainScore>
        {
            MakeScore(RseDomain.Environmental, 50, sectorWeight: 1.00m),
        };

        Assert.Single(singleDomainWithSectorWeightOne);
        Assert.Equal(1.00m, singleDomainWithSectorWeightOne[0].SectorWeight);

        var label = QuestPdfReportGenerator.ComputeSectorWeightingLabel(defaultSectorWeightingApplied: true);

        Assert.Contains("par défaut", label, StringComparison.OrdinalIgnoreCase);
    }

    // docs/specs/rapport-pdf.md, cas 19 : 300 dpi rapportés à la taille d'affichage réelle dans
    // le document, pas à une taille de rendu arbitraire — ReportTests.Cas19 confirme séparément
    // que le PNG embarqué n'est pas rééchantillonné (UseOriginalImage), donc que cette résolution
    // de rendu est bien celle qui atteint le papier.
    [Fact]
    public void Le_radar_atteint_au_moins_300_dpi_a_la_taille_d_affichage()
    {
        const float PointsPerInch = 72f;
        var effectiveDpi = RadarChartRenderer.RenderedSizePx / (QuestPdfReportGenerator.RadarImageSize / PointsPerInch);

        Assert.True(
            effectiveDpi >= 300f,
            $"Le radar s'affiche à {effectiveDpi:F0} dpi, sous le seuil de lisibilité à l'impression de 300 dpi.");
    }
}
