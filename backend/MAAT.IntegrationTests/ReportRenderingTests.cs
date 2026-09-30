using System.Text;
using MAAT.Application.DTOs;
using MAAT.Application.UseCases;
using MAAT.Domain.Entities;
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

    // Section 04 construite comme en production, par ReportSustainabilityBuilder, à partir
    // d'entités du Domain : ce que le générateur reçoit réellement.
    private static ReportSustainability Sustainability(
        bool complete,
        int siteCount = 2,
        VsmeDisclosure[]? omitted = null,
        bool micro = false,
        string? longText = null)
    {
        var now = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var company = new Company("Menuiserie Dupont & Fils", "1623Z", micro ? CompanySizeRange.Micro : CompanySizeRange.Medium, "Île-de-France");

        var statement = new VsmeStatement(company.Id, 2025, now);
        statement.Update(new VsmeStatementValues
        {
            ReportingBasis = ReportingBasis.Consolidated,
            LegalForm = "SAS",
            TotalAssetsEur = complete ? 12_800_000 : null,
            PrimaryCountry = "France",
            EmployeeCountUnit = EmployeeCountUnit.Headcount,
            OmittedDisclosures = omitted ?? [],
            Subsidiaries = [new VsmeSubsidiary("Dupont Agencement", "4 allée des Artisans 77100 Meaux")],
            Certifications = [new VsmeCertification("PEFC chaîne de contrôle", "PEFC France", new DateOnly(2024, 5, 14), null)],
            HasPractices = true,
            HasPolicies = true,
            PoliciesPublic = true,
            HasFutureInitiatives = true,
            HasTargets = complete ? true : null,
            PracticesDescription = longText ?? "Tri des chutes de bois, chaudière biomasse alimentée par les copeaux, plan de formation annuel.",
            CoveredTopics = [SustainabilityTopic.ClimateChange, SustainabilityTopic.CircularEconomy, SustainabilityTopic.Workforce],
            PollutionReportingApplicable = true,
            PollutionReportUrl = "https://www.georisques.gouv.fr/",
            Pollutants = [new VsmePollutant("Composés organiques volatils", PollutionMedium.Air, 1.24, "t")],
            CircularEconomyApplied = true,
            CircularEconomyDescription = longText ?? "Réemploi des chutes en petits objets, reprise des palettes par le fournisseur.",
            MaterialFlowsDescription = "Bois massif 820 t, panneaux 310 t.",
            MinimumWageMet = true,
            CorruptionConvictions = 0,
        }, now);

        var indicators = new RseIndicators(company.Id, 2025);
        indicators.Update(new RseIndicatorValues
        {
            RevenueEur = 18_400_000,
            ElectricityRenewableMwh = 120,
            ElectricityNonRenewableMwh = 310,
            FuelsRenewableMwh = 540,
            FuelsNonRenewableMwh = 95,
            Scope1Tco2e = 64.2,
            Scope2LocationTco2e = complete ? 21.5 : null,
            WaterWithdrawalM3 = 1_450,
            HazardousWasteTons = 2.4,
            NonHazardousWasteTons = 188,
            RecyclingRatePct = 71,
            PermanentEmployees = micro ? 7 : 112,
            TemporaryEmployees = micro ? 1 : 9,
            FemaleEmployees = micro ? 3 : 34,
            MaleEmployees = micro ? 5 : 87,
            RecordableAccidents = 3,
            HoursWorked = 182_000,
            WorkFatalities = 0,
            CollectiveBargainingPct = 100,
            TrainingHoursPerEmployee = 16.5,
            GenderEqualityIndex = 86,
            LocalSuppliersPct = 42,
        });

        var previous = new RseIndicators(company.Id, 2024);
        previous.Update(new RseIndicatorValues { RevenueEur = 17_100_000, Scope1Tco2e = 71, RecyclingRatePct = 64, GenderEqualityIndex = 84 });

        var sites = Enumerable.Range(1, siteCount).Select(i =>
        {
            var site = new CompanySite(company.Id, new CompanySiteDetails(
                i == 1 ? "Atelier principal" : $"Dépôt {i}",
                $"{i} rue des Scieries 77100 Meaux",
                i == 1 ? SiteTenure.Owned : SiteTenure.Leased,
                InOrNearSensitiveArea: i == 1,
                SensitiveAreaName: i == 1 ? "Natura 2000 « Boucles de la Marne »" : null), now.AddMinutes(i));
            site.Locate(48.96 + (i / 1000.0), 2.88 + (i / 1000.0), $"{i} Rue des Scieries 77100 Meaux");
            return site;
        }).ToList();

        return ReportSustainabilityBuilder.Build(new ReportSustainabilityBuilder.Input(2025, company, statement, indicators, null, previous, sites));
    }

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
        Sustainability(complete: true),
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
            Sustainability = null,
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

    // docs/specs/abonnement.md, cas 31 : rapport de l'offre Starter, tel que DiagnosticService
    // le transmet (ni scores de domaine, ni plan d'actions, ni indicateurs). Une seule page,
    // plus courte que le rapport complet, et déterministe comme lui.
    [Fact]
    public void Cas31_Rapport_Starter_reduit_a_la_page_de_garde_et_aux_mentions()
    {
        var starter = Rich() with
        {
            DomainScores = [],
            Recommendations = [],
            TotalRecommendationCount = 0,
            ActionStatusSummary = new ReportActionStatusSummary(0, 0, 0, 0),
            PreviousDomainScores = null,
            Sustainability = null,
            FullReport = false,
        };
        var generator = new QuestPdfReportGenerator();

        var bytes = generator.Generate(starter);

        AssertValidPdf(bytes);
        Assert.True(bytes.Length < generator.Generate(Rich()).Length);
        Assert.Equal(bytes, generator.Generate(starter));
    }

    // rapport-pdf.md, section 7 : logo de l'entreprise en page de garde, tel que
    // SkiaLogoImageProcessor l'a normalisé à l'envoi. Logo carré, très allongé ou très haut,
    // avec une raison sociale longue : la page de garde tient toujours, et le document reste
    // déterministe — le logo fait partie des données d'entrée (section 3).
    [Theory]
    [InlineData(200, 200)]
    [InlineData(600, 40)]
    [InlineData(40, 600)]
    public void Cas32_Logo_de_l_entreprise_en_page_de_garde(int width, int height)
    {
        var logo = new SkiaLogoImageProcessor().NormalizeToPng(CompanyLogoTests.MakeImage(width, height));
        var withLogo = Rich() with
        {
            CompanyName = "Société Coopérative Ouvrière de Production des Travaux Publics et du Bâtiment de la Vallée",
            CompanyLogoPng = logo,
        };
        var generator = new QuestPdfReportGenerator();

        var bytes = generator.Generate(withLogo);

        AssertValidPdf(bytes);
        Assert.True(bytes.Length > generator.Generate(withLogo with { CompanyLogoPng = null }).Length);
        Assert.Equal(bytes, generator.Generate(withLogo with { CompanyLogoPng = [.. logo] }));
    }

    // docs/specs/norme-volontaire.md, section 6 et cas 15 : chaque variante de la section 04 se
    // met en page sans lever (rapport conforme ou partiel, information omise, micro-entreprise,
    // cinquante sites, descriptions longues) et reste déterministe.
    [Theory]
    [InlineData("conforme")]
    [InlineData("partiel")]
    [InlineData("omise")]
    [InlineData("micro")]
    [InlineData("cinquante-sites")]
    [InlineData("textes-longs")]
    public void Section_04_norme_volontaire_se_met_en_page(string scenario)
    {
        var sustainability = scenario switch
        {
            "conforme" => Sustainability(complete: true),
            "partiel" => Sustainability(complete: false),
            "omise" => Sustainability(complete: true, omitted: [VsmeDisclosure.B3, VsmeDisclosure.B11]),
            "micro" => Sustainability(complete: false, micro: true),
            "cinquante-sites" => Sustainability(complete: true, siteCount: 50),
            "textes-longs" => Sustainability(complete: true, longText: string.Concat(Enumerable.Repeat("Description détaillée de la démarche de l'entreprise, étape par étape. ", 28))),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        var data = Rich() with { Sustainability = sustainability };
        var generator = new QuestPdfReportGenerator();

        var bytes = generator.Generate(data);

        AssertValidPdf(bytes);
        Assert.Equal(bytes, generator.Generate(data));
    }

    [Fact]
    public void Conformite_declaree_seulement_quand_le_module_est_complet()
    {
        var complete = Sustainability(complete: true);
        var partial = Sustainability(complete: false);

        Assert.True(complete.IsCompliant);
        Assert.False(partial.IsCompliant);
        Assert.Contains(partial.Disclosures, d => d.Code == VsmeDisclosure.B3 && d.Missing.Contains("Émissions Scope 2"));
        Assert.Equal(
            "Ce rapport de durabilité est établi selon le module de base (option A) de la norme volontaire européenne, règlement délégué (UE) 2026/1560.",
            QuestPdfReportGenerator.ComplianceStatement);
        Assert.StartsWith("3 informations sur 11 restent à compléter", QuestPdfReportGenerator.PartialReportLine(3, 11));
        Assert.StartsWith("1 information sur 11 reste à compléter", QuestPdfReportGenerator.PartialReportLine(1, 11));
    }

    // Le rapport ne laisse jamais une case blanche : chaque absence dit pourquoi.
    [Fact]
    public void Donnee_absente_nommee_selon_sa_nature()
    {
        Assert.Equal("Non renseigné", QuestPdfReportGenerator.AbsenceLabel(new ReportDatapoint("x", Absence: DatapointAbsence.NotProvided)));
        Assert.Equal("Facultatif (10 salariés ou moins)", QuestPdfReportGenerator.AbsenceLabel(new ReportDatapoint("x", Absence: DatapointAbsence.OptionalForMicro)));
        Assert.Equal(
            "Non applicable : pas d'obligation légale de publication",
            QuestPdfReportGenerator.AbsenceLabel(new ReportDatapoint("x", Absence: DatapointAbsence.NotApplicable, AbsenceNote: "pas d'obligation légale de publication")));

        var micro = Sustainability(complete: false, micro: true);
        var b3 = micro.Disclosures.Single(d => d.Code == VsmeDisclosure.B3);
        Assert.Equal(DatapointAbsence.OptionalForMicro, b3.Datapoints.Single(d => d.Label == "Émissions brutes Scope 2 (localisation)").Absence);
    }

    // B1 : code NACE dérivé du NAF, taux d'accidents pour 200 000 heures (cas 9 et 10), valeur
    // de l'exercice précédent (§14).
    [Fact]
    public void Donnees_derivees_de_la_section_04()
    {
        var sustainability = Sustainability(complete: true);
        var b1 = sustainability.Disclosures.Single(d => d.Code == VsmeDisclosure.B1);
        var b9 = sustainability.Disclosures.Single(d => d.Code == VsmeDisclosure.B9);
        var b3 = sustainability.Disclosures.Single(d => d.Code == VsmeDisclosure.B3);

        Assert.Equal("16.23 (NAF 1623Z)", b1.Datapoints.Single(d => d.Label == "Code NACE").Text);
        Assert.Equal(17_100_000, b1.Datapoints.Single(d => d.Label == "Chiffre d'affaires").PreviousValue);
        Assert.Equal(3.0 / 182_000 * 200_000, b9.Datapoints.Single(d => d.Label == "Taux d'accidents").Value);
        Assert.Equal(1_065, b3.Datapoints.Single(d => d.Label == "Consommation totale d'énergie").Value);
        Assert.Equal(2024, sustainability.PreviousYear);
    }
}
