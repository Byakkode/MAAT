using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using T = MAAT.Infrastructure.Pdf.ReportTheme;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 4. Le document que l'entreprise projette en réunion ou
// transmet à son donneur d'ordres : une page de garde qui dit l'essentiel en un coup d'œil,
// puis cinq sections numérotées — synthèse, évolution, plan d'actions, indicateurs, méthode —
// et les mentions. Pur : aucune I/O, aucune horloge système (GeneratedAt est une valeur du
// ReportData, jamais lue) — à ReportData identique, deux appels produisent des octets
// strictement identiques (section 3). QuestPdfBootstrapper.Configure() doit avoir été appelé
// avant le premier appel (licence + polices), ce que fait Program.cs au démarrage.
public sealed class QuestPdfReportGenerator : IReportGenerator
{
    // internal (pas private) : MAAT.IntegrationTests compare cette constante mot pour mot au
    // paragraphe exigé par rapport-pdf.md, section 4 — la seule vérification fiable de ce
    // texte précis, puisque QuestPDF dessine le texte par index de glyphe (CID) et qu'une
    // extraction depuis les octets du PDF ne le retrouverait pas (voir CapturingReportGenerator).
    internal const string MentionsText =
        "Ce rapport résulte d'une auto-évaluation déclarative réalisée par l'entreprise sur la " +
        "plateforme MAAT. Il ne constitue ni une certification, ni un audit, ni une notation par " +
        "un organisme tiers indépendant.";

    private const string FooterDisclaimer =
        "Auto-évaluation déclarative — ne constitue ni une certification, ni un audit.";

    // Identifiants des sections : cibles des liens du sommaire et source des numéros de page
    // qu'il affiche (BeginPageNumberOfSection).
    private static readonly (string Id, string Number, string Title)[] Sections =
    [
        ("synthese", "01", "Synthèse"),
        ("evolution", "02", "Évolution"),
        ("plan", "03", "Plan d'actions"),
        ("indicateurs", "04", "Indicateurs RSE"),
        ("methode", "05", "Comprendre votre score"),
    ];

    private const float CoverGutter = 48f;

    private const float EvolutionChartHeight = 130f;

    private const float SectionSpacing = 30f;

    // Radar : image affichée RadarImageSize × RadarImageSize au centre d'un bloc plus large,
    // qui laisse la place aux libellés d'axe posés en texte autour (cas 21). internal :
    // QuestPdfReportGeneratorTests vérifie la résolution d'impression (cas 19, seuil de 300 dpi)
    // à partir de cette taille et de RadarChartRenderer.RenderedSizePx.
    internal const float RadarImageSize = 144f;
    private const float RadarBlockWidth = 284f;
    private const float RadarBlockHeight = 204f;
    private const float RadarLabelWidth = 76f;
    private const float RadarLabelHeight = 24f;
    private const float RadarLabelGap = 5f;

    // Rayon du pentagone extérieur dans l'image, en points : RadarChartRenderer ne laisse
    // qu'une marge de 4 % de chaque côté (8 % du diamètre) pour les points de données.
    private const float RadarChartRadius = RadarImageSize / 2 * 0.92f;

    public byte[] Generate(ReportData data)
    {
        // DocumentMetadata.Default utilise DateTimeOffset.Now : le conserver casserait le
        // déterminisme du cas 10 (deux générations à quelques millisecondes d'écart produiraient
        // des métadonnées différentes). Dates fixées sur data.GeneratedAt, la seule horloge que
        // ce générateur connaît.
        var metadata = DocumentMetadata.Default;
        metadata.Title = $"Rapport RSE MAAT — {data.CompanyName}";
        metadata.Author = "MAAT";
        metadata.Creator = "MAAT";
        metadata.CreationDate = data.GeneratedAt;
        metadata.ModifiedDate = data.GeneratedAt;

        var view = new ReportView(data);
        var document = Document.Create(container =>
        {
            ComposeCoverPage(container, view);
            ComposeBodyPages(container, view);
        }).WithMetadata(metadata);

        return document.GeneratePdf();
    }

    // ---------------------------------------------------------------------------------------
    // Page de garde
    // ---------------------------------------------------------------------------------------

    private static void ComposeCoverPage(IDocumentContainer container, ReportView view)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(T.Surface);
            page.DefaultTextStyle(x => x.FontFamily(FontFamilies.Inter).FontSize(10).FontColor(T.Ink));

            // ScaleToFit : filet de sécurité — la page de garde tient toujours sur une page,
            // même avec une raison sociale hors norme ; sans lui, le sommaire basculerait sur
            // une deuxième page de garde.
            page.Content().ScaleToFit().Column(column =>
            {
                column.Item().Element(c => ComposeCoverBand(c, view));
                column.Item().PaddingHorizontal(CoverGutter).PaddingTop(28).Element(c => ComposeCoverScores(c, view));
                column.Item().PaddingHorizontal(CoverGutter).PaddingTop(16).Element(c => ComposeCoverHighlights(c, view));
                column.Item().PaddingHorizontal(CoverGutter).PaddingTop(26).Element(ComposeTableOfContents);
            });

            page.Footer().PaddingHorizontal(CoverGutter).PaddingBottom(26).Column(footer =>
            {
                footer.Item().PaddingBottom(8).LineHorizontal(0.75f).LineColor(T.Border);
                footer.Item().Row(row =>
                {
                    row.RelativeItem().Text(FooterDisclaimer).FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
                    row.AutoItem().Text($"Généré le {FrenchFormat.LongDate(view.Data.GeneratedAt)}")
                        .FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
                });
            });
        });
    }

    private static void ComposeCoverBand(IContainer container, ReportView view)
    {
        var data = view.Data;
        var white = Colors.White;

        container.Background(T.Sidebar).PaddingHorizontal(CoverGutter).PaddingTop(34).PaddingBottom(34).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("MAAT").FontFamily(FontFamilies.PoppinsBold).FontSize(20).FontColor(white);
                    text.Span(".").FontFamily(FontFamilies.PoppinsBold).FontSize(20).FontColor(T.Blue);
                });
                row.AutoItem().AlignMiddle().Text("Diagnostic RSE · VSME")
                    .FontFamily(FontFamilies.PoppinsMedium).FontSize(8.5f).FontColor(white.WithAlpha(0.7f)).LetterSpacing(0.04f);
            });

            column.Item().PaddingTop(54).Text("RAPPORT DE DIAGNOSTIC RSE")
                .FontFamily(FontFamilies.PoppinsMedium).FontSize(9).FontColor(white.WithAlpha(0.7f)).LetterSpacing(0.14f);

            column.Item().PaddingTop(6).Text(data.CompanyName)
                .FontFamily(FontFamilies.PoppinsBold).FontSize(CoverTitleSize(data.CompanyName)).FontColor(white).LineHeight(1.1f);

            column.Item().PaddingTop(6).Text(view.SectorLine)
                .FontFamily(FontFamilies.Inter).FontSize(11).FontColor(white.WithAlpha(0.82f));

            column.Item().PaddingTop(20).Row(row =>
            {
                row.Spacing(8);
                foreach (var chip in new[]
                {
                    CompanySizeRangeLabels.For(data.SizeRange),
                    data.Region,
                    $"Diagnostic du {FrenchFormat.LongDate(data.CompletedAt)}",
                })
                {
                    row.AutoItem()
                        .Border(0.75f).BorderColor(white.WithAlpha(0.28f)).CornerRadius(12)
                        .PaddingVertical(4).PaddingHorizontal(10)
                        .Text(chip).FontFamily(FontFamilies.Inter).FontSize(8.5f).FontColor(white.WithAlpha(0.9f));
                }
            });
        });
    }

    // Raison sociale en grand, mais pas au point de pousser le sommaire hors de la page : une
    // SCOP ou une SAS au nom complet dépasse facilement soixante caractères.
    private static float CoverTitleSize(string companyName) => companyName.Length switch
    {
        <= 28 => 32f,
        <= 56 => 26f,
        _ => 21f,
    };

    private static void ComposeCoverScores(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Row(row =>
        {
            row.Spacing(16);

            // Jauge du score global.
            row.ConstantItem(196).Element(Card).Column(column =>
            {
                column.Item().Text("Score global").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                column.Item().PaddingTop(12).AlignCenter().Width(132).Height(132).Layers(layers =>
                {
                    layers.PrimaryLayer().Svg(ReportCharts.ScoreGauge(data.GlobalScore, "#1565FF", "#EFF6FF", 132, 12));
                    layers.Layer().AlignCenter().AlignMiddle().Column(center =>
                    {
                        center.Item().AlignCenter().Text(FrenchFormat.Score(data.GlobalScore))
                            .FontFamily(FontFamilies.PoppinsBold).FontSize(38).FontColor(T.Ink).LineHeight(1f);
                        center.Item().AlignCenter().Text("sur 100").FontFamily(FontFamilies.InterLight).FontSize(8.5f).FontColor(T.InkMuted);
                    });
                });
                column.Item().PaddingTop(12).AlignCenter()
                    .Background(T.KpiBlue).CornerRadius(10).PaddingVertical(3).PaddingHorizontal(10)
                    .Text(data.GlobalScoreLabel).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(T.BlueText);

                if (view.GlobalDelta is { } delta)
                {
                    column.Item().PaddingTop(8).AlignCenter().Text(text =>
                    {
                        text.AlignCenter();
                        text.Span($"{FrenchFormat.SignedPoints(delta)} pts").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(8.5f).FontColor(DeltaColor(delta));
                        text.Span($" depuis {FrenchFormat.MonthYear(view.PreviousCompletedAt!.Value)}").FontSize(8.5f).FontColor(T.InkMuted);
                    });
                }
            });

            // Profil par domaine.
            row.RelativeItem().Element(Card).Column(column =>
            {
                column.Item().Row(header =>
                {
                    header.RelativeItem().Text("Profil par domaine").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                    header.AutoItem().AlignBottom().Text("score sur 100").FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
                });

                column.Item().PaddingTop(10).Column(list =>
                {
                    list.Spacing(11);
                    foreach (var domainScore in data.DomainScores)
                    {
                        list.Item().Column(item =>
                        {
                            item.Item().Row(line =>
                            {
                                line.AutoItem().AlignMiddle().Element(c => Dot(c, T.DomainColor(domainScore.Domain), 7));
                                line.RelativeItem().PaddingLeft(6).Text(RseDomainLabels.For(domainScore.Domain)).FontSize(9);
                                line.AutoItem().Text(FrenchFormat.Score(domainScore.Score)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9.5f);
                            });
                            item.Item().PaddingTop(4).Element(c => ScoreBar(c, domainScore.Score, T.DomainColor(domainScore.Domain), 6));
                        });
                    }
                });
            });
        });
    }

    private static void ComposeCoverHighlights(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Row(row =>
        {
            row.Spacing(12);

            if (view.Best is { } best)
            {
                row.RelativeItem().Element(c => KpiCard(c, T.Green, T.KpiGreen, "Point fort",
                    RseDomainLabels.For(best.Domain), $"{FrenchFormat.Score(best.Score)} / 100 — {ScoreLabel.For(FrenchFormat.Round(best.Score))}"));
            }

            if (view.Worst is { } worst)
            {
                row.RelativeItem().Element(c => KpiCard(c, T.Orange, T.KpiAmber, "Priorité de progrès",
                    RseDomainLabels.For(worst.Domain), $"{FrenchFormat.Score(worst.Score)} / 100 — {ScoreLabel.For(FrenchFormat.Round(worst.Score))}"));
            }

            var summary = data.ActionStatusSummary;
            row.RelativeItem().Element(c => KpiCard(c, T.Blue, T.KpiBlue, "Plan d'actions",
                summary.Total == 0 ? "Aucune action requise" : $"{summary.Done} sur {summary.Total} terminée{(summary.Done > 1 ? "s" : string.Empty)}",
                summary.Total == 0 ? "Toutes les pratiques évaluées sont couvertes" : $"{summary.InProgress} en cours · {summary.Blocked} bloquée{(summary.Blocked > 1 ? "s" : string.Empty)}"));
        });
    }

    private static void ComposeTableOfContents(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(8).Text("Dans ce rapport").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);

            foreach (var (id, number, title) in Sections)
            {
                column.Item().SectionLink(id).BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(6).Row(row =>
                {
                    row.ConstantItem(28).Text(number).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9.5f).FontColor(T.Blue);
                    row.RelativeItem().Text(title).FontSize(9.5f);
                    row.AutoItem().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontFamily(FontFamilies.Inter).FontSize(9.5f).FontColor(T.InkMuted));
                        text.Span("p. ");
                        text.BeginPageNumberOfSection(id);
                    });
                });
            }
        });
    }

    // ---------------------------------------------------------------------------------------
    // Pages de contenu
    // ---------------------------------------------------------------------------------------

    private static void ComposeBodyPages(IDocumentContainer container, ReportView view)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(42);
            page.MarginTop(30);
            page.MarginBottom(26);
            page.PageColor(T.Surface);
            page.DefaultTextStyle(x => x.FontFamily(FontFamilies.Inter).FontSize(9.5f).FontColor(T.Ink));

            page.Header().Element(c => ComposeRunningHeader(c, view.Data));
            page.Footer().Element(ComposeRunningFooter);

            page.Content().Column(column =>
            {
                // Pas de saut de page forcé entre les sections : chacune commence là où la
                // précédente s'arrête, sauf s'il ne reste pas la place de son en-tête et d'un
                // premier bloc (EnsureSpace) — un titre de section n'est jamais seul en bas de
                // page, et le document ne contient pas de demi-pages blanches. L'espacement est
                // porté par le bas de la section précédente plutôt que par le haut de la
                // suivante : en fin de page il disparaît, au lieu de décaler le titre d'une
                // section qui ouvre une nouvelle page.
                column.Item().PaddingBottom(SectionSpacing).Element(c => ComposeSynthesis(c, view));
                column.Item().EnsureSpace(260).PaddingBottom(SectionSpacing).Element(c => ComposeEvolution(c, view));
                column.Item().EnsureSpace(300).PaddingBottom(SectionSpacing).Element(c => ComposeActionPlan(c, view));
                column.Item().EnsureSpace(360).PaddingBottom(SectionSpacing).Element(c => ComposeIndicators(c, view));
                column.Item().EnsureSpace(300).PaddingBottom(16).Element(c => ComposeMethodology(c, view));
                column.Item().ShowEntire().Element(c => ComposeMentions(c, view));
            });
        });
    }

    private static void ComposeRunningHeader(IContainer container, ReportData data)
    {
        container.PaddingBottom(18).Column(column =>
        {
            column.Item().PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("MAAT").FontFamily(FontFamilies.PoppinsBold).FontSize(10).FontColor(T.Blue);
                    text.Span($"   Rapport de diagnostic RSE · {data.CompanyName}").FontSize(8.5f).FontColor(T.InkMuted);
                });
                row.AutoItem().AlignBottom().Text($"Diagnostic du {FrenchFormat.LongDate(data.CompletedAt)}").FontSize(8.5f).FontColor(T.InkMuted);
            });
            column.Item().LineHorizontal(0.75f).LineColor(T.Border);
        });
    }

    private static void ComposeRunningFooter(IContainer container)
    {
        container.PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Text(FooterDisclaimer).FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontFamily(FontFamilies.Inter).FontSize(7.5f).FontColor(T.InkMuted));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void SectionHeader(IContainer container, string id, string lead)
    {
        var (_, number, title) = Sections.Single(s => s.Id == id);

        container.Section(id).PaddingBottom(14).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().AlignMiddle().Background(T.KpiBlue).CornerRadius(6).PaddingVertical(3).PaddingHorizontal(7)
                    .Text(number).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(T.BlueText);
                row.RelativeItem().PaddingLeft(10).AlignMiddle().Text(title).FontFamily(FontFamilies.PoppinsBold).FontSize(18);
            });
            column.Item().PaddingTop(6).Text(lead).FontSize(9.5f).FontColor(T.InkMuted).LineHeight(1.35f);
        });
    }

    private static void SubHeading(IContainer container, string text) =>
        container.PaddingBottom(8).Text(text).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(11);

    // ---------------------------------------------------------------------------------------
    // 01 — Synthèse
    // ---------------------------------------------------------------------------------------

    private static void ComposeSynthesis(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Column(column =>
        {
            column.Item().Element(c => SectionHeader(c, "synthese",
                "L'essentiel du diagnostic : où en est l'entreprise sur chacun des cinq domaines RSE, et où se trouvent ses principaux leviers de progrès."));

            // Lecture en une phrase, rédigée à partir des chiffres : le paragraphe qu'on lit à
            // voix haute en ouverture de réunion.
            column.Item().Background(T.KpiBlue).BorderLeft(3).BorderColor(T.Blue).CornerRadius(4).Padding(12)
                .Text(view.Narrative).FontSize(10).LineHeight(1.45f);

            column.Item().PaddingTop(18).Row(row =>
            {
                row.Spacing(16);
                row.ConstantItem(RadarBlockWidth).Element(Card).Column(radar =>
                {
                    radar.Item().Text("Radar des cinq domaines").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                    radar.Item().AlignCenter().Element(c => ComposeRadar(c, data));
                    radar.Item().AlignCenter().Text("Échelle fixe de 0 à 100 sur chaque axe.")
                        .FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
                });

                row.RelativeItem().Column(side =>
                {
                    side.Spacing(12);
                    if (view.Strengths.Count == 0)
                    {
                        side.Item().Element(Card).Text(
                            "Trop peu de domaines notés pour distinguer points forts et axes de progrès sans répétition.")
                            .FontSize(9).FontColor(T.InkMuted);
                        return;
                    }

                    side.Item().Element(c => DomainListCard(c, "Points forts", "Les domaines les plus avancés, sur lesquels s'appuyer.", T.Green, T.KpiGreen, T.GreenText, view.Strengths));
                    side.Item().Element(c => DomainListCard(c, "Axes de progrès", "Les domaines où chaque action pèse le plus sur le score.", T.Orange, T.KpiAmber, T.AmberText, view.Weaknesses));
                });
            });

            column.Item().PaddingTop(20).Element(c => SubHeading(c, "Scores par domaine"));
            column.Item().Element(c => ComposeDomainScoreList(c, view));
        });
    }

    // Cas 21 : les libellés d'axe sont posés en texte QuestPDF autour de l'image — jamais
    // incrustés dans le PNG — donc sélectionnables et déterministes indépendamment du rendu
    // SkiaSharp. RadarAxisLayout garantit le même ordre et le même angle que les axes dessinés.
    private static void ComposeRadar(IContainer container, ReportData data)
    {
        var scoreByDomain = data.DomainScores.ToDictionary(d => d.Domain, d => d.Score);
        var radarPng = RadarChartRenderer.Render(scoreByDomain);

        container.Width(RadarBlockWidth - 28).Height(RadarBlockHeight).Layers(layers =>
        {
            // UseOriginalImage : embarque les pixels du PNG sans rééchantillonnage (cas 19).
            layers.PrimaryLayer().AlignCenter().AlignMiddle()
                .Width(RadarImageSize).Height(RadarImageSize)
                .Image(radarPng).UseOriginalImage(true);

            foreach (var axis in RadarAxisLayout.Compute())
            {
                // Sommet de l'axe sur le pentagone extérieur, puis libellé posé à l'extérieur :
                // à droite du sommet (texte aligné à gauche) pour les axes de droite, à gauche
                // (texte aligné à droite) pour ceux de gauche, au-dessus ou au-dessous pour les
                // autres — le texte s'écarte du graphique au lieu de déborder du bloc.
                var vertexX = axis.DirectionX * RadarChartRadius;
                var vertexY = axis.DirectionY * RadarChartRadius;
                var side = axis.DirectionX switch { > 0.5f => 1, < -0.5f => -1, _ => 0 };
                var centerX = side == 0 ? vertexX : vertexX + (side * (RadarLabelGap + (RadarLabelWidth / 2)));
                var centerY = side != 0
                    ? vertexY
                    : vertexY + (Math.Sign(axis.DirectionY) * (RadarLabelGap + (RadarLabelHeight / 2)));
                var hasScore = scoreByDomain.TryGetValue(axis.Domain, out var score);

                layers.Layer().AlignCenter().AlignMiddle()
                    .OffsetX(centerX)
                    .OffsetY(centerY)
                    .Width(RadarLabelWidth)
                    .Height(RadarLabelHeight)
                    .Column(label =>
                    {
                        var name = label.Item().Text(T.DomainShortLabel(axis.Domain)).FontSize(8).FontColor(T.InkMuted);
                        var value = label.Item().Text(hasScore ? FrenchFormat.Score(score) : "—")
                            .FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9.5f).FontColor(T.Ink);
                        switch (side)
                        {
                            case 1:
                                name.AlignLeft();
                                value.AlignLeft();
                                break;
                            case -1:
                                name.AlignRight();
                                value.AlignRight();
                                break;
                            default:
                                name.AlignCenter();
                                value.AlignCenter();
                                break;
                        }
                    });
            }
        });
    }

    private static void DomainListCard(IContainer container, string title, string subtitle, Color accent, Color tint, Color titleColor, IReadOnlyList<ReportDomainScore> domains)
    {
        container.Background(tint).BorderLeft(4).BorderColor(accent).CornerRadius(6).Padding(12).Column(column =>
        {
            column.Item().Text(title).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f).FontColor(titleColor);
            column.Item().PaddingTop(2).Text(subtitle).FontSize(8).FontColor(T.InkMuted);
            column.Item().PaddingTop(8).Column(list =>
            {
                list.Spacing(6);
                foreach (var domain in domains)
                {
                    list.Item().Row(row =>
                    {
                        row.AutoItem().AlignMiddle().Element(c => Dot(c, T.DomainColor(domain.Domain), 7));
                        row.RelativeItem().PaddingLeft(6).Text(RseDomainLabels.For(domain.Domain)).FontSize(9.5f);
                        row.AutoItem().Text(text =>
                        {
                            text.Span(FrenchFormat.Score(domain.Score)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10);
                            text.Span(" / 100").FontSize(8).FontColor(T.InkMuted);
                        });
                    });
                }
            });
        });
    }

    private static void ComposeDomainScoreList(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Element(Card).Column(column =>
        {
            for (var i = 0; i < data.DomainScores.Count; i++)
            {
                var domainScore = data.DomainScores[i];
                var rounded = FrenchFormat.Round(domainScore.Score);
                var item = column.Item();
                if (i > 0)
                {
                    item = item.BorderTop(0.75f).BorderColor(T.Border);
                }

                item.PaddingVertical(8).Row(row =>
                {
                    row.ConstantItem(150).Column(name =>
                    {
                        name.Item().Row(line =>
                        {
                            line.AutoItem().AlignMiddle().Element(c => Dot(c, T.DomainColor(domainScore.Domain), 7));
                            line.RelativeItem().PaddingLeft(6).Text(RseDomainLabels.For(domainScore.Domain)).FontSize(9.5f);
                        });
                        name.Item().PaddingLeft(13).Text($"Poids sectoriel {FrenchFormat.Percentage(domainScore.SectorWeight)}")
                            .FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
                    });
                    row.RelativeItem().PaddingHorizontal(10).AlignMiddle().Element(c => ScoreBar(c, domainScore.Score, T.DomainColor(domainScore.Domain), 8));
                    row.ConstantItem(34).AlignMiddle().AlignRight().Text(rounded.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .FontFamily(FontFamilies.PoppinsSemiBold).FontSize(12);
                    row.ConstantItem(118).PaddingLeft(10).AlignMiddle().Text(ScoreLabel.For(rounded)).FontSize(8.5f).FontColor(T.InkMuted);
                    row.ConstantItem(46).AlignMiddle().AlignRight().Element(c =>
                    {
                        if (view.DomainDelta(domainScore.Domain) is { } delta)
                        {
                            DeltaChip(c, delta);
                        }
                    });
                });
            }
        });
    }

    // ---------------------------------------------------------------------------------------
    // 02 — Évolution
    // ---------------------------------------------------------------------------------------

    private static void ComposeEvolution(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Column(column =>
        {
            column.Item().Element(c => SectionHeader(c, "evolution",
                "La trajectoire de l'entreprise d'un diagnostic à l'autre : c'est la progression dans le temps, plus que le score d'un jour, qui témoigne d'une démarche RSE."));

            if (data.History.Count < 2)
            {
                column.Item().Element(Card).Row(row =>
                {
                    row.ConstantItem(4).Background(T.Blue).CornerRadius(2);
                    row.RelativeItem().PaddingLeft(12).Column(text =>
                    {
                        text.Item().Text("Premier diagnostic : votre point de référence").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                        text.Item().PaddingTop(4).Text(
                            "Ce diagnostic est le premier réalisé par l'entreprise sur MAAT. Il servira de base de comparaison : " +
                            "un nouveau diagnostic dans six à douze mois, une fois les premières actions engagées, " +
                            "fera apparaître ici la courbe de progression et l'écart domaine par domaine.")
                            .FontSize(9).FontColor(T.InkMuted).LineHeight(1.4f);
                    });
                });
                return;
            }

            column.Item().ShowEntire().Row(row =>
            {
                row.Spacing(16);
                row.RelativeItem(3).Element(Card).Column(chart =>
                {
                    chart.Item().Text("Score global").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                    chart.Item().PaddingTop(10).Row(plot =>
                    {
                        plot.ConstantItem(20).Height(EvolutionChartHeight).Layers(axis =>
                        {
                            axis.PrimaryLayer();
                            foreach (var tick in new[] { 100, 75, 50, 25, 0 })
                            {
                                axis.Layer().OffsetY(ReportCharts.EvolutionTickY(tick, EvolutionChartHeight) - 4.5f).Height(9)
                                    .Text(tick.ToString(System.Globalization.CultureInfo.InvariantCulture)).FontSize(6.5f).FontColor(T.InkMuted);
                            }
                        });
                        plot.RelativeItem().Height(EvolutionChartHeight).Svg(size => ReportCharts.EvolutionLine(
                            [.. data.History.Select(h => h.GlobalScore)], size.Width, size.Height, "#1565FF", "#E5E7EB"));
                    });
                    chart.Item().PaddingLeft(20).PaddingTop(6).Row(labels =>
                    {
                        foreach (var point in data.History)
                        {
                            labels.RelativeItem().Column(label =>
                            {
                                label.Item().AlignCenter().Text(FrenchFormat.Score(point.GlobalScore)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9);
                                label.Item().AlignCenter().Text(FrenchFormat.MonthYear(point.CompletedAt)).FontSize(7).FontColor(T.InkMuted);
                            });
                        }
                    });
                });

                row.RelativeItem(2).Element(Card).Column(table =>
                {
                    table.Item().Text("Écart par domaine").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                    table.Item().PaddingTop(2).Text($"Depuis le diagnostic du {FrenchFormat.LongDate(view.PreviousCompletedAt!.Value)}")
                        .FontSize(7.5f).FontColor(T.InkMuted);
                    table.Item().PaddingTop(8).Column(list =>
                    {
                        foreach (var domainScore in data.DomainScores)
                        {
                            list.Item().BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(5).Row(line =>
                            {
                                line.AutoItem().AlignMiddle().Element(c => Dot(c, T.DomainColor(domainScore.Domain), 6));
                                line.RelativeItem().PaddingLeft(6).Text(T.DomainShortLabel(domainScore.Domain)).FontSize(8.5f);
                                var previous = data.PreviousDomainScores?.GetValueOrDefault(domainScore.Domain);
                                line.ConstantItem(52).AlignRight().Text(text =>
                                {
                                    text.Span(previous is null ? "—" : FrenchFormat.Score(previous.Value)).FontSize(8.5f).FontColor(T.InkMuted);
                                    text.Span(" → ").FontSize(8.5f).FontColor(T.InkMuted);
                                    text.Span(FrenchFormat.Score(domainScore.Score)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(8.5f);
                                });
                                line.ConstantItem(40).AlignRight().AlignMiddle().Element(c =>
                                {
                                    if (view.DomainDelta(domainScore.Domain) is { } delta)
                                    {
                                        DeltaChip(c, delta);
                                    }
                                });
                            });
                        }
                    });
                });
            });
        });
    }

    // ---------------------------------------------------------------------------------------
    // 03 — Plan d'actions
    // ---------------------------------------------------------------------------------------

    private static void ComposeActionPlan(IContainer container, ReportView view)
    {
        var data = view.Data;
        var summary = data.ActionStatusSummary;

        container.Column(column =>
        {
            column.Item().Element(c => SectionHeader(c, "plan",
                "Les actions recommandées, classées par priorité : gain attendu sur le score, pondéré par l'importance du domaine pour le secteur, rapporté à l'effort nécessaire."));

            if (data.Recommendations.Count == 0)
            {
                column.Item().Background(T.KpiGreen).BorderLeft(4).BorderColor(T.Green).CornerRadius(6).Padding(14).Column(text =>
                {
                    text.Item().Text("Aucune action requise").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(11).FontColor(T.GreenText);
                    text.Item().PaddingTop(4).Text(
                        "Aucune recommandation n'a été déclenchée par ce diagnostic : l'entreprise couvre déjà l'ensemble " +
                        "des bonnes pratiques évaluées dans ce référentiel.").FontSize(9.5f).LineHeight(1.4f);
                });
                return;
            }

            column.Item().PaddingBottom(20).Element(c => ComposeStatusOverview(c, summary));

            var priorities = data.Recommendations.Where(r => !r.IsCompleted).Take(3).ToList();
            if (priorities.Count > 0)
            {
                column.Item().EnsureSpace(160).Element(c => SubHeading(c, "Par où commencer"));
                column.Item().PaddingBottom(20).ShowEntire().Row(row =>
                {
                    row.Spacing(12);
                    foreach (var recommendation in priorities)
                    {
                        row.RelativeItem().Element(c => PriorityCard(c, recommendation));
                    }

                    for (var i = priorities.Count; i < 3; i++)
                    {
                        row.RelativeItem();
                    }
                });
            }

            column.Item().EnsureSpace(120).Element(c => SubHeading(c, "Plan d'actions détaillé"));
            column.Item().Element(c => ComposeActionTable(c, data.Recommendations));

            column.Item().PaddingTop(8).Text(
                data.TotalRecommendationCount > data.Recommendations.Count
                    ? $"{data.Recommendations.Count} actions prioritaires affichées sur {data.TotalRecommendationCount} au total. Le plan complet est consultable dans l'espace MAAT, rubrique Plan d'actions."
                    : $"{data.TotalRecommendationCount} action{(data.TotalRecommendationCount > 1 ? "s" : string.Empty)} au total. Le suivi détaillé est consultable dans l'espace MAAT, rubrique Plan d'actions.")
                .FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
        });
    }

    private static void ComposeStatusOverview(IContainer container, ReportActionStatusSummary summary)
    {
        var completion = summary.Total == 0 ? 0m : (decimal)summary.Done / summary.Total;

        container.Element(Card).Row(row =>
        {
            row.ConstantItem(120).Column(left =>
            {
                left.Item().Text(text =>
                {
                    text.Span(summary.Done.ToString(System.Globalization.CultureInfo.InvariantCulture)).FontFamily(FontFamilies.PoppinsBold).FontSize(26);
                    text.Span($" / {summary.Total}").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(12).FontColor(T.InkMuted);
                });
                left.Item().Text($"actions terminées · {FrenchFormat.Percentage(completion)}").FontSize(8.5f).FontColor(T.InkMuted);
            });

            row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(right =>
            {
                right.Item().Height(10).CornerRadius(5).Row(bar =>
                {
                    foreach (var status in StatusOrder)
                    {
                        var count = CountFor(summary, status);
                        if (count > 0)
                        {
                            bar.RelativeItem(count).Background(T.StatusBar(status));
                        }
                    }
                });

                right.Item().PaddingTop(8).Row(legend =>
                {
                    legend.Spacing(14);
                    foreach (var status in StatusOrder)
                    {
                        legend.AutoItem().Row(entry =>
                        {
                            entry.AutoItem().AlignMiddle().Element(c => Dot(c, T.StatusBar(status), 7));
                            entry.AutoItem().PaddingLeft(5).Text(text =>
                            {
                                text.Span(T.Status(status).Label).FontSize(8.5f).FontColor(T.InkMuted);
                                text.Span($" {CountFor(summary, status)}").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(8.5f);
                            });
                        });
                    }
                });
            });
        });
    }

    private static readonly ActionItemStatus[] StatusOrder =
        [ActionItemStatus.Done, ActionItemStatus.InProgress, ActionItemStatus.Blocked, ActionItemStatus.Planned];

    private static int CountFor(ReportActionStatusSummary summary, ActionItemStatus status) => status switch
    {
        ActionItemStatus.Planned => summary.Planned,
        ActionItemStatus.InProgress => summary.InProgress,
        ActionItemStatus.Blocked => summary.Blocked,
        ActionItemStatus.Done => summary.Done,
        _ => 0,
    };

    private static void PriorityCard(IContainer container, ReportRecommendation recommendation)
    {
        container.Element(Card).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().Width(20).Height(20).Background(T.Blue).CornerRadius(10).AlignCenter().AlignMiddle()
                    .Text(recommendation.PriorityRank.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(Colors.White);
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Element(c => DomainChip(c, recommendation.Domain));
            });

            column.Item().PaddingTop(10).Text(recommendation.ActionText).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9.5f).LineHeight(1.3f);

            if (!string.IsNullOrWhiteSpace(recommendation.DetailText))
            {
                // ClampLines ne reste qu'un filet de sécurité : Excerpt coupe déjà à la fin
                // d'une phrase, pour ne jamais imprimer un conseil tronqué au milieu d'un mot.
                column.Item().PaddingTop(6).Text(Excerpt(recommendation.DetailText)).FontSize(8).FontColor(T.InkMuted).LineHeight(1.4f).ClampLines(8);
            }

            // Effort et gain sur deux lignes : côte à côte, « Important » et « +12,5 pts » ne
            // tiennent pas dans le tiers de page d'une carte.
            column.Item().PaddingTop(10).BorderTop(0.75f).BorderColor(T.Border).PaddingTop(8).Column(footer =>
            {
                footer.Item().Element(c => EffortIndicator(c, recommendation.EffortLevel));
                footer.Item().PaddingTop(4).Text(text =>
                {
                    text.Span("Gain estimé sur le domaine ").FontSize(7.5f).FontColor(T.InkMuted);
                    text.Span($"+{FrenchFormat.Number(recommendation.ImpactPoints, 1)} pts").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(8).FontColor(T.GreenText);
                });
            });
        });
    }

    // Colonne plutôt que Table : chaque action est une ligne insécable (PreventPageBreak) —
    // l'ancienne table coupait le libellé d'une action entre deux pages. L'en-tête est répété
    // en haut de chaque page (Decoration.Before).
    private static void ComposeActionTable(IContainer container, IReadOnlyList<ReportRecommendation> recommendations)
    {
        container.Decoration(decoration =>
        {
            decoration.Before().Background(T.Background).BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(6).PaddingHorizontal(8).Row(row =>
            {
                ActionColumns(row,
                    c => HeaderLabel(c, "#"),
                    c => HeaderLabel(c, "Action"),
                    c => HeaderLabel(c, "Domaine"),
                    c => HeaderLabel(c, "Effort"),
                    c => HeaderLabel(c, "Statut"));
            });

            decoration.Content().Column(column =>
            {
                foreach (var recommendation in recommendations)
                {
                    column.Item().PreventPageBreak().BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(7).PaddingHorizontal(8).Row(row =>
                    {
                        ActionColumns(row,
                            c => c.Text(recommendation.PriorityRank.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                .FontFamily(FontFamilies.PoppinsSemiBold).FontSize(8.5f).FontColor(T.InkMuted),
                            c => c.Column(action =>
                            {
                                action.Item().Text(recommendation.ActionText).FontSize(9).LineHeight(1.25f)
                                    .FontColor(recommendation.IsCompleted ? T.InkMuted : T.Ink);
                                var tracking = TrackingLine(recommendation);
                                if (tracking is not null)
                                {
                                    action.Item().PaddingTop(2).Text(tracking).FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
                                }
                            }),
                            c => c.Row(domain =>
                            {
                                domain.AutoItem().PaddingTop(3).Element(d => Dot(d, T.DomainColor(recommendation.Domain), 6));
                                domain.RelativeItem().PaddingLeft(5).Text(T.DomainShortLabel(recommendation.Domain)).FontSize(8.5f);
                            }),
                            c => c.Element(e => EffortIndicator(e, recommendation.EffortLevel)),
                            c => c.Element(e => StatusBadge(e, recommendation.Status)));
                    });
                }
            });
        });
    }

    private static void ActionColumns(RowDescriptor row, params Action<IContainer>[] cells)
    {
        row.ConstantItem(20).Element(cells[0]);
        row.RelativeItem().PaddingRight(10).Element(cells[1]);
        row.ConstantItem(82).Element(cells[2]);
        row.ConstantItem(72).Element(cells[3]);
        row.ConstantItem(62).Element(cells[4]);
    }

    private static string? TrackingLine(ReportRecommendation recommendation)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(recommendation.AssignedTo))
        {
            parts.Add($"Responsable : {recommendation.AssignedTo.Trim()}");
        }

        if (recommendation.DueDate is { } dueDate)
        {
            parts.Add($"Échéance : {FrenchFormat.ShortDate(dueDate)}");
        }

        return parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }

    // ---------------------------------------------------------------------------------------
    // 04 — Indicateurs RSE
    // ---------------------------------------------------------------------------------------

    private static readonly (string Group, string Hex)[] IndicatorGroups =
    [
        ("Environnement", ReportTheme.DomainHex[RseDomain.Environmental]),
        ("Social", ReportTheme.DomainHex[RseDomain.Social]),
        ("Achats responsables", ReportTheme.DomainHex[RseDomain.Procurement]),
        ("Économique", "#1565FF"), // --color-blue-maat : pas de domaine RSE dédié
    ];

    private static void ComposeIndicators(IContainer container, ReportView view)
    {
        var indicators = view.Data.Indicators;

        container.Column(column =>
        {
            column.Item().Element(c => SectionHeader(c, "indicateurs", indicators is null
                ? "Les données chiffrées de l'entreprise — énergie, émissions, eau, déchets, effectifs, santé-sécurité, formation — telles que les demande le module de base du standard VSME."
                : $"Les données chiffrées déclarées par l'entreprise pour l'année {indicators.Year} — énergie, émissions, eau, déchets, effectifs, santé-sécurité, formation — telles que les demande le module de base du standard VSME."));

            if (indicators is null)
            {
                column.Item().Background(T.KpiBlue).BorderLeft(4).BorderColor(T.Blue).CornerRadius(6).Padding(14).Column(text =>
                {
                    text.Item().Text("Aucun indicateur renseigné").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f).FontColor(T.BlueText);
                    text.Item().PaddingTop(4).Text(
                        "Les indicateurs quantitatifs complètent le diagnostic qualitatif : ils se saisissent dans l'espace " +
                        "MAAT, rubrique Indicateurs, et apparaîtront dans ce rapport à sa prochaine génération.")
                        .FontSize(9).LineHeight(1.4f);
                });
                return;
            }

            // Deux rangées plutôt que deux colonnes : les cartes d'une même rangée prennent la
            // même hauteur, et une rangée n'est jamais coupée entre deux pages.
            foreach (var pair in IndicatorGroups.Chunk(2))
            {
                column.Item().PaddingBottom(14).ShowEntire().Row(row =>
                {
                    row.Spacing(14);
                    foreach (var group in pair)
                    {
                        row.RelativeItem().Element(c => IndicatorCard(c, indicators, group));
                    }
                });
            }

            column.Item().Text(indicators.PreviousYear is { } previousYear
                    ? $"Valeurs déclarées par l'entreprise, non vérifiées par un tiers. Évolution calculée par rapport à {previousYear} lorsque la donnée existe ; en vert, une évolution favorable."
                    : "Valeurs déclarées par l'entreprise, non vérifiées par un tiers. L'évolution apparaîtra dès que les indicateurs de l'année précédente seront renseignés.")
                .FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
        });
    }

    private static void IndicatorCard(IContainer container, ReportIndicators indicators, (string Group, string Hex) group)
    {
        var items = indicators.Items.Where(i => i.Group == group.Group).ToList();
        var accent = Color.FromHex(group.Hex);

        container.Element(Card).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().Width(4).Height(14).Background(accent).CornerRadius(2);
                row.RelativeItem().PaddingLeft(8).Text(group.Group).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
                row.AutoItem().AlignBottom().Text($"{items.Count(i => i.Value is not null)} / {items.Count} renseignés")
                    .FontFamily(FontFamilies.InterLight).FontSize(7.5f).FontColor(T.InkMuted);
            });

            column.Item().PaddingTop(6).Column(list =>
            {
                foreach (var indicator in items)
                {
                    list.Item().BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(6).Column(entry =>
                    {
                        entry.Item().Row(line =>
                        {
                            line.RelativeItem().Text(indicator.Label).FontSize(8.5f).FontColor(indicator.Value is null ? T.InkMuted : T.Ink);
                            if (indicator.Value is { } value)
                            {
                                line.AutoItem().Text(text =>
                                {
                                    text.Span(FrenchFormat.Number(value, value >= 100 ? 0 : 1)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9);
                                    text.Span($" {indicator.Unit}").FontSize(7.5f).FontColor(T.InkMuted);
                                });
                                line.ConstantItem(46).AlignRight().AlignMiddle().Element(c => IndicatorTrend(c, indicator));
                            }
                            else
                            {
                                line.AutoItem().Text("Non renseigné").FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
                                line.ConstantItem(46);
                            }
                        });

                        // Mini-jauge pour les taux bornés (pourcentages, index sur 100) : la
                        // valeur se lit d'un coup d'œil sans connaître l'échelle.
                        if (indicator.Value is { } ratio && indicator.Unit is "%" or "/100")
                        {
                            entry.Item().PaddingTop(4).PaddingRight(46).Element(c =>
                                ScoreBar(c, (decimal)Math.Clamp(ratio, 0, 100), accent, 4));
                        }
                    });
                }
            });
        });
    }

    // Évolution par rapport à l'année précédente : en points pour les taux (un écart relatif
    // sur un pourcentage prête à confusion), en pourcentage sinon. Couleur selon le sens
    // favorable de l'indicateur, neutre quand il n'en a pas (chiffre d'affaires, effectif).
    private static void IndicatorTrend(IContainer container, ReportIndicator indicator)
    {
        if (indicator.Value is not { } value || indicator.PreviousValue is not { } previous)
        {
            return;
        }

        var isRate = indicator.Unit is "%" or "/100";
        double change;
        string label;
        if (isRate)
        {
            change = value - previous;
            label = $"{SignedNumber(change)} pt";
        }
        else
        {
            if (previous == 0)
            {
                return;
            }

            change = (value - previous) / Math.Abs(previous) * 100;
            label = $"{SignedNumber(change)} %";
        }

        var color = Math.Abs(change) < 0.05 || indicator.HigherIsBetter is null
            ? T.InkMuted
            : (change > 0) == indicator.HigherIsBetter.Value ? T.GreenText : T.AmberText;

        container.Text(label).FontFamily(FontFamilies.PoppinsMedium).FontSize(7.5f).FontColor(color);
    }

    private static string SignedNumber(double value)
    {
        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        return rounded switch
        {
            > 0 => "+" + FrenchFormat.Number(rounded, 1),
            < 0 => FrenchFormat.Number(rounded, 1),
            _ => "0",
        };
    }

    // ---------------------------------------------------------------------------------------
    // 05 — Comprendre votre score
    // ---------------------------------------------------------------------------------------

    private static void ComposeMethodology(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Column(column =>
        {
            column.Item().Element(c => SectionHeader(c, "methode",
                "D'où vient le chiffre : la méthode de calcul, les référentiels mobilisés et le détail qui permet à tout lecteur de refaire l'opération."));

            column.Item().ShowEntire().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(c => MethodStep(c, "1", "Vos réponses",
                    "Chaque question est notée de 0 à 5 et porte un poids selon son importance. Elle est rattachée à l'un des cinq domaines RSE."));
                row.RelativeItem().Element(c => MethodStep(c, "2", "Un score par domaine",
                    "Somme des réponses pondérées, rapportée au maximum atteignable : Σ(réponse × poids) ÷ Σ(5 × poids) × 100."));
                row.RelativeItem().Element(c => MethodStep(c, "3", "La pondération sectorielle",
                    "Le score global combine les cinq domaines selon des coefficients propres au secteur d'activité (code NAF) : les enjeux prioritaires diffèrent d'un métier à l'autre."));
            });

            column.Item().PaddingTop(10).PaddingBottom(18).ShowEntire().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(c => Referential(c, "VSME", "Standard européen de reporting de durabilité volontaire pour les PME (EFRAG)."));
                row.RelativeItem().Element(c => Referential(c, "ISO 26000", "Lignes directrices internationales relatives à la responsabilité sociétale."));
                row.RelativeItem().Element(c => Referential(c, "GRI", "Standards internationaux de reporting de durabilité (Global Reporting Initiative)."));
            });

            column.Item().EnsureSpace(200).Element(c => SubHeading(c, "Détail du calcul"));
            // La formule précède le tableau plutôt que de le suivre : placée dessous, elle
            // partait seule en haut de la page suivante dès que le tableau finissait la page.
            column.Item().Text(ComputeSectorWeightingLabel(data.DefaultSectorWeightingApplied)).FontSize(8.5f).FontColor(T.InkMuted);
            column.Item().PaddingTop(2).PaddingBottom(8).Text(
                "Score = numérateur ÷ dénominateur × 100. Contribution = score × pondération sectorielle. " +
                "La somme des contributions donne le score global.")
                .FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
            column.Item().Element(c => ComposeDomainDetailTable(c, data));
        });
    }

    private static void MethodStep(IContainer container, string number, string title, string body)
    {
        container.Element(Card).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().Width(20).Height(20).Background(T.KpiBlue).CornerRadius(10).AlignCenter().AlignMiddle()
                    .Text(number).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(T.BlueText);
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Text(title).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9.5f);
            });
            column.Item().PaddingTop(8).Text(body).FontSize(8).FontColor(T.InkMuted).LineHeight(1.4f);
        });
    }

    private static void Referential(IContainer container, string name, string description)
    {
        container.Background(T.Background).CornerRadius(8).PaddingVertical(8).PaddingHorizontal(10).Column(column =>
        {
            column.Item().Text(name).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(T.BlueText);
            column.Item().PaddingTop(2).Text(description).FontSize(7.5f).FontColor(T.InkMuted).LineHeight(1.35f);
        });
    }

    private static void ComposeDomainDetailTable(IContainer container, ReportData data)
    {
        container.Border(0.75f).BorderColor(T.Border).CornerRadius(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.6f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.45f);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.45f);
                columns.RelativeColumn(1.45f);
            });

            table.Header(header =>
            {
                foreach (var (label, alignRight) in new[]
                {
                    ("Domaine", false), ("Score", true), ("Numérateur", true), ("Dénominateur", true), ("Pondération", true), ("Contribution", true),
                })
                {
                    var cell = header.Cell().Background(T.Background).BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(7).PaddingHorizontal(10);
                    (alignRight ? cell.AlignRight() : cell).Element(c => HeaderLabel(c, label));
                }
            });

            foreach (var domainScore in data.DomainScores)
            {
                table.Cell().Element(BodyCell).Row(row =>
                {
                    row.AutoItem().AlignMiddle().Element(c => Dot(c, T.DomainColor(domainScore.Domain), 7));
                    row.RelativeItem().PaddingLeft(6).Text(RseDomainLabels.For(domainScore.Domain)).FontSize(9);
                });
                table.Cell().Element(BodyCell).AlignRight().Text(FrenchFormat.Score(domainScore.Score)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9);
                table.Cell().Element(BodyCell).AlignRight().Text(FrenchFormat.Number(domainScore.Numerator)).FontSize(9);
                table.Cell().Element(BodyCell).AlignRight().Text(FrenchFormat.Number(domainScore.Denominator)).FontSize(9);
                table.Cell().Element(BodyCell).AlignRight().Text(FrenchFormat.Percentage(domainScore.SectorWeight)).FontSize(9);
                table.Cell().Element(BodyCell).AlignRight().Text(FrenchFormat.Number(domainScore.Contribution, 1)).FontSize(9);
            }

            table.Cell().ColumnSpan(5).Background(T.KpiBlue).PaddingVertical(8).PaddingHorizontal(10)
                .Text("Score global (somme des contributions)").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(9).FontColor(T.BlueText);
            table.Cell().Background(T.KpiBlue).PaddingVertical(8).PaddingHorizontal(10).AlignRight()
                .Text($"{FrenchFormat.Score(data.GlobalScore)} / 100").FontFamily(FontFamilies.PoppinsBold).FontSize(10).FontColor(T.BlueText);
        });

        static IContainer BodyCell(IContainer cell) =>
            cell.BorderBottom(0.75f).BorderColor(T.Border).PaddingVertical(7).PaddingHorizontal(10).AlignMiddle();
    }

    private static void ComposeMentions(IContainer container, ReportView view)
    {
        var data = view.Data;

        container.Background(T.Background).CornerRadius(8).Padding(14).Column(column =>
        {
            column.Item().Text("Mentions").FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f);
            column.Item().PaddingTop(6).Text(MentionsText).FontSize(9).LineHeight(1.4f);

            column.Item().PaddingTop(8).Column(meta =>
            {
                meta.Spacing(2);
                foreach (var line in new[]
                {
                    $"Le score reflète les réponses au jour de la complétion du diagnostic, le {FrenchFormat.LongDate(data.CompletedAt)}.",
                    "L'avancement du plan d'actions et les indicateurs RSE sont présentés tels qu'ils étaient enregistrés à la date de génération.",
                    $"Référentiel de questions, version {data.ReferentialVersion}.",
                    $"Date de génération : {FrenchFormat.LongDate(data.GeneratedAt)}.",
                })
                {
                    meta.Item().Text(line).FontFamily(FontFamilies.InterLight).FontSize(8).FontColor(T.InkMuted);
                }
            });
        });
    }

    // ---------------------------------------------------------------------------------------
    // Composants
    // ---------------------------------------------------------------------------------------

    // Carte de la charte : fond blanc, bordure --color-border, rayon 12 px (ici 10 pt),
    // padding interne 20 px (ici 14 pt, à l'échelle d'une page A4).
    private static IContainer Card(IContainer container) =>
        container.Border(0.75f).BorderColor(T.Border).CornerRadius(10).Background(T.Surface).Padding(14);

    // Carte KPI de la charte : bordure gauche colorée de 4 px et fond teinté très subtil.
    private static void KpiCard(IContainer container, Color accent, Color tint, string eyebrow, string value, string caption)
    {
        container.Background(tint).BorderLeft(4).BorderColor(accent).CornerRadius(6).PaddingVertical(10).PaddingHorizontal(12).Column(column =>
        {
            column.Item().Text(eyebrow.ToUpperInvariant()).FontFamily(FontFamilies.PoppinsMedium).FontSize(7).FontColor(T.InkMuted).LetterSpacing(0.08f);
            column.Item().PaddingTop(3).Text(value).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(10.5f).LineHeight(1.2f);
            column.Item().PaddingTop(2).Text(caption).FontSize(8).FontColor(T.InkMuted);
        });
    }

    private static void Dot(IContainer container, Color color, float size) =>
        container.Width(size).Height(size).Background(color).CornerRadius(size / 2);

    private static void ScoreBar(IContainer container, decimal score, Color color, float height)
    {
        var filled = (float)Math.Clamp(score, 0m, 100m);

        container.Height(height).Background(T.Background).CornerRadius(height / 2).Row(row =>
        {
            if (filled > 0)
            {
                row.RelativeItem(filled).Background(color).CornerRadius(height / 2);
            }

            if (filled < 100)
            {
                row.RelativeItem(100 - filled);
            }
        });
    }

    private static void DeltaChip(IContainer container, int delta)
    {
        var (text, fill) = delta switch
        {
            > 0 => (T.GreenText, T.Green.WithAlpha(0.12f)),
            < 0 => (T.AmberText, T.Orange.WithAlpha(0.14f)),
            _ => (T.InkMuted, T.Background),
        };

        container.AlignMiddle().Background(fill).CornerRadius(8).PaddingVertical(2).PaddingHorizontal(6)
            .Text(FrenchFormat.SignedPoints(delta)).FontFamily(FontFamilies.PoppinsSemiBold).FontSize(7.5f).FontColor(text);
    }

    private static Color DeltaColor(int delta) => delta switch
    {
        > 0 => T.GreenText,
        < 0 => T.AmberText,
        _ => T.InkMuted,
    };

    private static void DomainChip(IContainer container, RseDomain domain)
    {
        container.AlignLeft().AlignTop().Background(T.DomainColor(domain).WithAlpha(0.12f)).CornerRadius(8).PaddingVertical(2).PaddingHorizontal(7)
            .Text(T.DomainShortLabel(domain)).FontFamily(FontFamilies.PoppinsMedium).FontSize(7.5f).FontColor(T.Ink);
    }

    private static void StatusBadge(IContainer container, ActionItemStatus status)
    {
        var (label, text, fill) = T.Status(status);
        container.AlignLeft().AlignTop().Background(fill).CornerRadius(8).PaddingVertical(2).PaddingHorizontal(7)
            .Text(label).FontFamily(FontFamilies.PoppinsMedium).FontSize(7.5f).FontColor(text);
    }

    // Trois barres, comme un indicateur de signal : on compare l'effort de deux actions sans
    // lire le libellé.
    private static void EffortIndicator(IContainer container, EffortLevel effortLevel)
    {
        var level = effortLevel switch
        {
            EffortLevel.Low => 1,
            EffortLevel.Medium => 2,
            _ => 3,
        };
        var label = effortLevel switch
        {
            EffortLevel.Low => "Faible",
            EffortLevel.Medium => "Modéré",
            _ => "Important",
        };

        container.AlignTop().Row(row =>
        {
            row.AutoItem().AlignMiddle().Row(bars =>
            {
                bars.Spacing(2);
                for (var i = 1; i <= 3; i++)
                {
                    bars.ConstantItem(4).AlignBottom().Height(3 + (i * 2)).Background(i <= level ? T.InkMuted : T.Border).CornerRadius(1);
                }
            });
            row.AutoItem().PaddingLeft(6).AlignMiddle().Text(label).FontSize(8.5f);
        });
    }

    private static void HeaderLabel(IContainer container, string text) =>
        container.Text(text).FontFamily(FontFamilies.PoppinsMedium).FontSize(8).FontColor(T.InkMuted);

    // ---------------------------------------------------------------------------------------
    // Règles de contenu, testées directement par MAAT.IntegrationTests
    // ---------------------------------------------------------------------------------------

    // docs/specs/rapport-pdf.md, section 4 : les deux listes ne sont affichées que si elles
    // sont disjointes. Avec moins de quatre domaines notés (un domaine sans aucune question
    // active en est exclu, scoring.md cas 7), Take(2) des meilleurs et Take(2) des derniers se
    // chevauchaient — un même domaine apparaissait à la fois comme point fort et comme axe
    // d'amélioration.
    internal static (IReadOnlyList<ReportDomainScore> Strengths, IReadOnlyList<ReportDomainScore> Weaknesses) ComputeStrengthsAndWeaknesses(
        IReadOnlyList<ReportDomainScore> domainScores)
    {
        if (domainScores.Count < 4)
        {
            return ([], []);
        }

        // Départage déterministe par l'ordre de l'énumération RseDomain (comme
        // RecommendationEngine.Prioritize départage par code) : à scores égaux, deux
        // générations doivent produire la même liste (section 3).
        var ordered = domainScores.OrderByDescending(d => d.Score).ThenBy(d => (int)d.Domain).ToList();
        var strengths = ordered.Take(2).ToList();
        var weaknesses = ordered.AsEnumerable().Reverse().Take(2).ToList();
        return (strengths, weaknesses);
    }

    // Détail d'une action sur une carte « Par où commencer » : les premières phrases qui
    // tiennent dans maxLength caractères. Sans fin de phrase exploitable, coupe au dernier
    // mot entier et termine par une ellipse. Le détail complet reste consultable dans l'espace
    // MAAT ; le rapport, lui, ne doit jamais s'arrêter au milieu d'un mot.
    internal static string Excerpt(string text, int maxLength = 200)
    {
        text = text.Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        var sentenceEnd = text.LastIndexOfAny(['.', '!', '?'], maxLength - 1);
        if (sentenceEnd >= maxLength / 3)
        {
            return text[..(sentenceEnd + 1)];
        }

        var wordEnd = text.LastIndexOf(' ', maxLength - 1);
        return text[..(wordEnd > 0 ? wordEnd : maxLength)].TrimEnd(',', ';', ':', ' ') + " …";
    }

    // docs/specs/rapport-pdf.md, section 4. Le paramètre vient de
    // Diagnostic.DefaultSectorWeightingApplied, décidé une fois à la complétion
    // (questionnaire.md, section 6, cas 13) — jamais de ReportDomainScore.SectorWeight, qui ne
    // distingue pas les deux cas quand un seul domaine est actif (renormalisation de
    // scoring.md, cas 7). Hors repli par défaut, la pondération peut venir du code NAF exact
    // ou de sa section (SectorWeightRepository) : le libellé reste volontairement générique
    // plutôt que d'affirmer un niveau que le diagnostic n'a pas enregistré.
    internal static string ComputeSectorWeightingLabel(bool defaultSectorWeightingApplied) =>
        defaultSectorWeightingApplied
            ? "Secteur non répertorié : pondération par défaut appliquée (0,20 sur chaque domaine)."
            : "Pondération sectorielle appliquée : coefficients propres au secteur d'activité de l'entreprise, déterminés à partir de son code NAF.";

    // Valeurs dérivées du ReportData, calculées une fois pour toutes les sections.
    private sealed class ReportView
    {
        public ReportView(ReportData data)
        {
            Data = data;
            (Strengths, Weaknesses) = ComputeStrengthsAndWeaknesses(data.DomainScores);

            var ordered = data.DomainScores.OrderByDescending(d => d.Score).ThenBy(d => (int)d.Domain).ToList();
            Best = ordered.Count >= 2 ? ordered[0] : null;
            Worst = ordered.Count >= 2 ? ordered[^1] : null;

            if (data.History.Count >= 2)
            {
                var previous = data.History[^2];
                PreviousCompletedAt = previous.CompletedAt;
                GlobalDelta = FrenchFormat.Round(data.GlobalScore) - FrenchFormat.Round(previous.GlobalScore);
            }

            var nafLabel = NafLabels.For(data.SectorCode);
            SectorLine = nafLabel is null ? $"Code NAF {data.SectorCode}" : $"{data.SectorCode} · {nafLabel}";
            Narrative = BuildNarrative();
        }

        public ReportData Data { get; }

        public IReadOnlyList<ReportDomainScore> Strengths { get; }

        public IReadOnlyList<ReportDomainScore> Weaknesses { get; }

        public ReportDomainScore? Best { get; }

        public ReportDomainScore? Worst { get; }

        public DateTimeOffset? PreviousCompletedAt { get; }

        public int? GlobalDelta { get; }

        public string SectorLine { get; }

        public string Narrative { get; }

        public int? DomainDelta(RseDomain domain) =>
            Data.PreviousDomainScores is { } previous && previous.TryGetValue(domain, out var previousScore)
                ? FrenchFormat.Round(Data.DomainScores.Single(d => d.Domain == domain).Score) - FrenchFormat.Round(previousScore)
                : null;

        // Même vocabulaire que le tableau de bord : une trajectoire, jamais un jugement.
        private string BuildNarrative()
        {
            var data = Data;
            var sentences = new List<string>
            {
                $"Avec un score global de {FrenchFormat.Score(data.GlobalScore)} sur 100, {data.CompanyName} se situe au niveau « {data.GlobalScoreLabel} ».",
            };

            if (Best is not null && Worst is not null && Best.Domain != Worst.Domain)
            {
                sentences.Add(
                    $"Son domaine le plus avancé est {RseDomainLabels.For(Best.Domain)} ({FrenchFormat.Score(Best.Score)}/100) ; " +
                    $"la marge de progression la plus nette concerne {RseDomainLabels.For(Worst.Domain)} ({FrenchFormat.Score(Worst.Score)}/100).");
            }

            if (GlobalDelta is { } delta && PreviousCompletedAt is { } previousAt)
            {
                sentences.Add(delta switch
                {
                    > 0 => $"Le score a progressé de {delta} point{(delta > 1 ? "s" : string.Empty)} depuis le diagnostic du {FrenchFormat.LongDate(previousAt)}.",
                    < 0 => $"Le score a reculé de {-delta} point{(delta < -1 ? "s" : string.Empty)} depuis le diagnostic du {FrenchFormat.LongDate(previousAt)}.",
                    _ => $"Le score est stable depuis le diagnostic du {FrenchFormat.LongDate(previousAt)}.",
                });
            }

            var total = data.ActionStatusSummary.Total;
            if (total > 0)
            {
                sentences.Add($"{total} action{(total > 1 ? "s sont proposées" : " est proposée")} pour poursuivre la démarche, classée{(total > 1 ? "s" : string.Empty)} par priorité en section 03.");
            }

            return string.Join(" ", sentences);
        }
    }
}
