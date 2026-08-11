using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 4 : les sept blocs, dans l'ordre. Pur — aucune I/O,
// aucune horloge système ici (GeneratedAt est une valeur du ReportData, jamais lue) : à
// ReportData identique, deux appels à Generate produisent des octets strictement identiques
// (section 3). QuestPdfBootstrapper.Configure() doit avoir été appelé avant le premier appel
// (licence + polices), toujours vrai en usage normal puisque Program.cs l'invoque au démarrage.
public sealed class QuestPdfReportGenerator : IReportGenerator
{
    // internal (pas private) : MAAT.IntegrationTests compare cette constante mot pour mot au
    // paragraphe exigé par rapport-pdf.md, section 4 — la seule vérification fiable de ce
    // texte précis, puisque QuestPDF/SkiaSharp dessine le texte par index de glyphe (CID) et
    // qu'une extraction depuis les octets du PDF produit ne le retrouverait pas (voir
    // CapturingReportGenerator).
    internal const string MentionsText =
        "Ce rapport résulte d'une auto-évaluation déclarative réalisée par l'entreprise sur la " +
        "plateforme MAAT. Il ne constitue ni une certification, ni un audit, ni une notation par " +
        "un organisme tiers indépendant.";

    private static readonly Color TextColor = "#1E1E2D";
    private static readonly Color MutedColor = "#6B7280";
    private static readonly Color BorderColor = "#E5E7EB";
    private static readonly Color BlueMaat = "#1565FF";
    private static readonly Color BgColor = "#F8F9FC";

    public byte[] Generate(ReportData data)
    {
        // DocumentMetadata.Default utilise DateTimeOffset.Now (horloge système, heure locale) :
        // rester sur cette valeur par défaut casserait le déterminisme du cas 10 (deux
        // générations à quelques millisecondes d'écart produiraient des métadonnées PDF
        // différentes, donc des octets différents). CreationDate/ModifiedDate sont donc fixées
        // explicitement sur data.GeneratedAt, la seule horloge que ce générateur connaît.
        var metadata = DocumentMetadata.Default;
        metadata.Title = $"Rapport RSE MAAT — {data.CompanyName}";
        metadata.Author = "MAAT";
        metadata.Creator = "MAAT";
        metadata.CreationDate = data.GeneratedAt;
        metadata.ModifiedDate = data.GeneratedAt;

        var document = Document.Create(container => Compose(container, data)).WithMetadata(metadata);
        return document.GeneratePdf();
    }

    private static void Compose(IDocumentContainer container, ReportData data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(FontFamilies.Inter).FontSize(10).FontColor(TextColor));

            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(MutedColor));
                text.Span("MAAT — page ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });

            page.Content().Column(column =>
            {
                column.Item().Element(c => ComposeCover(c, data));
                column.Item().PageBreak();

                column.Item().Element(c => ComposeMethodology(c));
                column.Item().PaddingTop(16).Element(c => ComposeScoreAndRadar(c, data));
                column.Item().PaddingTop(16).Element(c => ComposeDomainDetail(c, data));
                column.Item().PaddingTop(16).Element(c => ComposeStrengthsAndWeaknesses(c, data));
                column.Item().PaddingTop(16).Element(c => ComposeActionPlan(c, data));

                column.Item().PageBreak();
                column.Item().Element(c => ComposeMentions(c, data));
            });
        });
    }

    private static void ComposeCover(IContainer container, ReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().PaddingBottom(24).Text("MAAT").FontFamily(FontFamilies.PoppinsBold).FontSize(28).FontColor(BlueMaat);

            column.Item().Text(data.CompanyName).FontFamily(FontFamilies.PoppinsBold).FontSize(20);
            column.Item().Text($"Secteur d'activité (code NAF) : {data.SectorCode}").FontFamily(FontFamilies.Inter).FontSize(11);
            column.Item().Text(CompanySizeRangeLabels.For(data.SizeRange)).FontFamily(FontFamilies.Inter).FontSize(11);
            column.Item().Text($"Région : {data.Region}").FontFamily(FontFamilies.Inter).FontSize(11);
            column.Item().Text($"Diagnostic complété le {FormatDate(data.CompletedAt)}").FontFamily(FontFamilies.Inter).FontSize(11);

            column.Item().PaddingTop(32).Background(BgColor).Padding(20).Column(score =>
            {
                score.Spacing(4);
                score.Item().AlignCenter().Text($"{RoundForDisplay(data.GlobalScore)} / 100")
                    .FontFamily(FontFamilies.PoppinsBold).FontSize(40).FontColor(BlueMaat);
                score.Item().AlignCenter().Text(data.GlobalScoreLabel)
                    .FontFamily(FontFamilies.PoppinsSemiBold).FontSize(16);
            });
        });
    }

    private static void ComposeMethodology(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Méthodologie").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);
            column.Item().Text(
                "Le diagnostic MAAT s'appuie sur trois référentiels RSE reconnus : le standard " +
                "européen VSME (Voluntary SME sustainability reporting Standard), la norme ISO 26000 " +
                "et le GRI (Global Reporting Initiative). Chaque question du questionnaire est " +
                "rattachée à l'un des cinq domaines RSE et pondérée selon son importance.")
                .FontFamily(FontFamilies.Inter).FontSize(9.5f).LineHeight(1.3f);
            column.Item().Text(
                "Le score d'un domaine est la somme des réponses pondérées par le poids de chaque " +
                "question, rapportée au maximum théorique atteignable, exprimée sur 100. Le score " +
                "global est la moyenne des cinq scores de domaine, pondérée par des coefficients " +
                "sectoriels propres au code NAF de l'entreprise : deux entreprises de secteurs " +
                "différents, avec des réponses identiques, obtiennent des scores globaux différents, " +
                "parce que leurs enjeux prioritaires ne sont pas les mêmes.")
                .FontFamily(FontFamilies.Inter).FontSize(9.5f).LineHeight(1.3f);
        });
    }

    private static void ComposeScoreAndRadar(IContainer container, ReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Score global et radar des cinq domaines").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);

            var scoreByDomain = data.DomainScores.ToDictionary(d => d.Domain, d => d.Score);
            var radarPng = RadarChartRenderer.Render(scoreByDomain);

            // UseOriginalImage : embarque les pixels du PNG sans rééchantillonnage, quelle
            // que soit la taille d'affichage ci-dessous — c'est ce qui garantit à la fois le
            // déterminisme (aucun retraitement dépendant de l'environnement) et la résolution
            // d'impression exigée par la section 5 (cas 19).
            column.Item().AlignCenter().Width(280, Unit.Point).Element(radarContainer =>
                radarContainer.Image(radarPng).UseOriginalImage(true));

            column.Item().Text("Échelle fixe de 0 à 100 sur les cinq axes.")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
        });
    }

    private static void ComposeDomainDetail(IContainer container, ReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Détail par domaine").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Domaine");
                    HeaderCell(header.Cell(), "Score /100");
                    HeaderCell(header.Cell(), "Pondération");
                    HeaderCell(header.Cell(), "Numérateur");
                    HeaderCell(header.Cell(), "Dénominateur");
                    HeaderCell(header.Cell(), "Contribution");
                });

                foreach (var domainScore in data.DomainScores)
                {
                    BodyCell(table.Cell(), RseDomainLabels.For(domainScore.Domain));
                    BodyCell(table.Cell(), RoundForDisplay(domainScore.Score).ToString());
                    BodyCell(table.Cell(), FormatPercentage(domainScore.SectorWeight));
                    BodyCell(table.Cell(), FormatDecimal(domainScore.Numerator));
                    BodyCell(table.Cell(), FormatDecimal(domainScore.Denominator));
                    BodyCell(table.Cell(), FormatDecimal(domainScore.Contribution));
                }
            });

            column.Item().Text(
                "Score = numérateur ÷ dénominateur × 100. Contribution = score × pondération " +
                "sectorielle. La somme des contributions donne le score global.")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
        });
    }

    private static void ComposeStrengthsAndWeaknesses(IContainer container, ReportData data)
    {
        // Départage déterministe par l'ordre de l'énumération RseDomain (comme
        // RecommendationEngine.Prioritize départage par code) : à scores égaux, deux
        // générations doivent produire la même liste (section 3).
        var ordered = data.DomainScores.OrderByDescending(d => d.Score).ThenBy(d => (int)d.Domain).ToList();
        var strengths = ordered.Take(2).ToList();
        var weaknesses = ordered.AsEnumerable().Reverse().Take(2).ToList();

        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Points forts et axes d'amélioration").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);

            column.Item().Row(row =>
            {
                row.Spacing(16);
                row.RelativeItem().Element(c => ComposeDomainList(c, "Points forts", strengths));
                row.RelativeItem().Element(c => ComposeDomainList(c, "Axes d'amélioration", weaknesses));
            });
        });
    }

    private static void ComposeDomainList(IContainer container, string title, IReadOnlyList<ReportDomainScore> domains)
    {
        container.Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(title).FontFamily(FontFamilies.PoppinsMedium).FontSize(11);
            foreach (var domain in domains)
            {
                column.Item().Text($"{RseDomainLabels.For(domain.Domain)} — {RoundForDisplay(domain.Score)} / 100")
                    .FontFamily(FontFamilies.Inter).FontSize(9.5f);
            }
        });
    }

    private static void ComposeActionPlan(IContainer container, ReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Plan d'actions").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);

            if (data.Recommendations.Count == 0)
            {
                column.Item().Text(
                    "Aucune recommandation n'a été déclenchée par ce diagnostic : l'entreprise " +
                    "couvre déjà l'ensemble des bonnes pratiques évaluées dans ce référentiel.")
                    .FontFamily(FontFamilies.Inter).FontSize(9.5f);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Action");
                    HeaderCell(header.Cell(), "Domaine");
                    HeaderCell(header.Cell(), "Effort");
                    HeaderCell(header.Cell(), "État");
                });

                foreach (var recommendation in data.Recommendations)
                {
                    BodyCell(table.Cell(), recommendation.ActionText);
                    BodyCell(table.Cell(), RseDomainLabels.For(recommendation.Domain));
                    BodyCell(table.Cell(), EffortLevelLabels.For(recommendation.EffortLevel));
                    BodyCell(table.Cell(), recommendation.IsCompleted ? "Terminée" : "À traiter");
                }
            });

            column.Item().Text(
                $"{data.Recommendations.Count} action(s) affichée(s) sur {data.TotalRecommendationCount} au total.")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
        });
    }

    private static void ComposeMentions(IContainer container, ReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Mentions").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(15);

            column.Item().Border(1).BorderColor(BorderColor).Padding(12).Text(MentionsText)
                .FontFamily(FontFamilies.Inter).FontSize(9.5f).LineHeight(1.35f);

            column.Item().PaddingTop(8).Text($"Date de génération : {FormatDate(data.GeneratedAt)}")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
            column.Item().Text($"Référentiel de questions, version {data.ReferentialVersion}.")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
            column.Item().Text($"Le score reflète les réponses au jour de la complétion du diagnostic, le {FormatDate(data.CompletedAt)}.")
                .FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(MutedColor);
        });
    }

    private static void HeaderCell(QuestPDF.Elements.Table.ITableCellContainer cell, string text) =>
        cell.Background(BgColor).Padding(4).Text(text).FontFamily(FontFamilies.PoppinsMedium).FontSize(9);

    private static void BodyCell(QuestPDF.Elements.Table.ITableCellContainer cell, string text) =>
        cell.BorderBottom(1).BorderColor(BorderColor).Padding(4).Text(text).FontFamily(FontFamilies.Inter).FontSize(9);

    // Identique à ScoringService.RoundForDisplay (MAAT.Domain) : arrondi au plus proche, .5
    // vers le haut. Dupliqué ici plutôt que réutilisé directement pour rester générique sur
    // n'importe quel decimal du rapport (score de domaine ou score global), comme le fait déjà
    // ScoringService.RoundForDisplay lui-même.
    private static int RoundForDisplay(decimal value) => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string FormatPercentage(decimal weight) => $"{RoundForDisplay(weight * 100)} %";

    private static string FormatDecimal(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // Format manuel plutôt qu'une culture "fr-FR" : évite toute dépendance aux données ICU
    // installées sur l'environnement cible (section 5 de rapport-pdf.md documente déjà le
    // même type de piège pour les polices — un VPS Linux minimal peut ne pas avoir la culture
    // française installée, sans erreur ni avertissement).
    private static string FormatDate(DateTimeOffset value) => value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
