using System.Text;
using MAAT.Application.DTOs;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 24 à 27. Générateur réel, sans base de données : chaque
// section du document a une branche « données absentes » (premier diagnostic, aucun
// indicateur, aucune action) et des cas limites de mise en page (libellés longs, un seul
// domaine, historique complet). QuestPDF lève une exception de mise en page quand un élément
// ne peut pas tenir sur une page — c'est ce que ces tests détectent, avant un client.
public class ReportRenderingTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 9, 21, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    public ReportRenderingTests() => QuestPdfBootstrapper.Configure();

    private static List<ReportDomainScore> FiveDomains() =>
    [
        new(RseDomain.Environmental, 36.36m, 0.10m, 40m, 110m),
        new(RseDomain.Social, 56.36m, 0.30m, 62m, 110m),
        new(RseDomain.Ethics, 45m, 0.30m, 36m, 80m),
        new(RseDomain.Procurement, 51.43m, 0.10m, 36m, 70m),
        new(RseDomain.Governance, 55m, 0.20m, 44m, 80m),
    ];

    private static List<ReportRecommendation> Recommendations(int count, ActionItemStatus status = ActionItemStatus.Planned) =>
        [.. Enumerable.Range(1, count).Select(i => new ReportRecommendation(
            i,
            $"Action recommandée numéro {i}, formulée comme une consigne concrète et datable",
            i % 2 == 0 ? "Détail de mise en œuvre : qui s'en charge, avec quel document, et comment vérifier que c'est fait." : null,
            (RseDomain)(i % 5),
            (EffortLevel)(i % 3),
            2.5m,
            i == 1 ? ActionItemStatus.Done : status,
            i == 2 ? "Claire Martin" : null,
            i == 2 ? CompletedAt.AddMonths(2) : null))];

    private static ReportData Rich() => new(
        "Menuiserie Dupont & Fils",
        "6201Z",
        CompanySizeRange.Medium,
        "Île-de-France",
        CompletedAt,
        50.19m,
        "Démarche structurée",
        false,
        FiveDomains(),
        Recommendations(20, ActionItemStatus.InProgress),
        35,
        new ReportActionStatusSummary(24, 6, 2, 3),
        [
            new(new DateTimeOffset(2025, 3, 12, 0, 0, 0, TimeSpan.Zero), 31m),
            new(new DateTimeOffset(2025, 10, 2, 0, 0, 0, TimeSpan.Zero), 38m),
            new(CompletedAt, 50.19m),
        ],
        new Dictionary<RseDomain, decimal> { [RseDomain.Environmental] = 30m, [RseDomain.Social] = 60m },
        new ReportIndicators(2025, 2024,
        [
            new("Environnement", "Émissions CO₂", "tCO₂eq/an", 182.4, 205.0, false),
            new("Environnement", "Consommation eau", "m³/an", null, null, false),
            new("Social", "Taux d'accidents du travail", "‰", 2.1, 1.8, false),
            new("Social", "Index égalité F/H", "/100", 86, 84, true),
            new("Achats responsables", "Fournisseurs locaux (< 100 km)", "%", 42, 40, true),
            new("Économique", "Chiffre d'affaires", "€", 18_400_000, 0, null),
        ]),
        GeneratedAt,
        "1.0");

    private static void AssertValidPdf(byte[] bytes)
    {
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public void Cas24_Rapport_complet_se_genere()
    {
        AssertValidPdf(new QuestPdfReportGenerator().Generate(Rich()));
    }

    // Premier diagnostic, aucun indicateur, aucune action suivie : chaque section doit
    // remplacer son contenu par un message explicite, pas lever ni rester vide.
    [Fact]
    public void Cas25_Premier_diagnostic_sans_indicateur_ni_suivi_se_genere()
    {
        var data = Rich() with
        {
            History = [new(CompletedAt, 50.19m)],
            PreviousDomainScores = null,
            Indicators = null,
            Recommendations = Recommendations(3),
            TotalRecommendationCount = 3,
            ActionStatusSummary = new ReportActionStatusSummary(2, 0, 0, 1),
        };

        AssertValidPdf(new QuestPdfReportGenerator().Generate(data));
    }

    // Cas limites de mise en page : un seul domaine actif (renormalisation de scoring.md,
    // cas 7), plan d'actions vide, toutes les actions terminées, historique de six points,
    // raison sociale et code NAF absents de la liste INSEE.
    [Theory]
    [InlineData("un-domaine")]
    [InlineData("aucune-action")]
    [InlineData("tout-termine")]
    [InlineData("historique-complet")]
    [InlineData("libelles-longs")]
    public void Cas26_Cas_limites_de_mise_en_page(string scenario)
    {
        var data = Rich();
        data = scenario switch
        {
            "un-domaine" => data with { DomainScores = [new(RseDomain.Social, 62m, 1.00m, 62m, 100m)], PreviousDomainScores = null },
            "aucune-action" => data with { Recommendations = [], TotalRecommendationCount = 0, ActionStatusSummary = new(0, 0, 0, 0) },
            "tout-termine" => data with { Recommendations = Recommendations(20, ActionItemStatus.Done), ActionStatusSummary = new(0, 0, 0, 20), TotalRecommendationCount = 20 },
            "historique-complet" => data with
            {
                History = [.. Enumerable.Range(0, 6).Select(i => new ReportHistoryPoint(CompletedAt.AddMonths(-6 * (5 - i)), 20m + (i * 12m)))],
            },
            "libelles-longs" => data with
            {
                CompanyName = "Société Coopérative Ouvrière de Production des Travaux Publics et du Bâtiment de la Vallée",
                SectorCode = "9999Z",
                Region = "Provence-Alpes-Côte d'Azur",
                Recommendations = [.. data.Recommendations.Select(r => r with { ActionText = string.Concat(Enumerable.Repeat(r.ActionText + " ", 4)), AssignedTo = "Responsable qualité, sécurité et environnement du site principal" })],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        AssertValidPdf(new QuestPdfReportGenerator().Generate(data));
    }

    // Le défaut réel : sur les données de production, le détail d'une action était tronqué
    // par ClampLines au milieu d'un mot (« ce que l… »).
    [Fact]
    public void Excerpt_s_arrete_a_la_fin_d_une_phrase_jamais_au_milieu_d_un_mot()
    {
        const string twoSentences =
            "Ajoutez à votre modèle de devis une rubrique listant explicitement les exclusions, les conditions et les limites de la prestation. " +
            "La plupart des litiges ne portent pas sur ce qui a été promis mais sur ce que le client croyait inclus.";
        var noSentenceEnd = string.Join(" ", Enumerable.Repeat("responsabilité", 30));

        Assert.Equal("Phrase courte.", QuestPdfReportGenerator.Excerpt("  Phrase courte.  "));
        Assert.EndsWith("de la prestation.", QuestPdfReportGenerator.Excerpt(twoSentences));
        Assert.EndsWith("responsabilité …", QuestPdfReportGenerator.Excerpt(noSentenceEnd));
        Assert.True(QuestPdfReportGenerator.Excerpt(noSentenceEnd).Length <= 202);
    }

    // Section 3 sans passer par l'API : le cas 10 (ReportDeterminismTests) le vérifie de bout
    // en bout, celui-ci isole le générateur — graphiques SVG et libellés NAF compris.
    [Fact]
    public void Cas27_Deux_generations_du_meme_ReportData_sont_identiques_octet_pour_octet()
    {
        var generator = new QuestPdfReportGenerator();

        Assert.Equal(generator.Generate(Rich()), generator.Generate(Rich()));
    }
}
