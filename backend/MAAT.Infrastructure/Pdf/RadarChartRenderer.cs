using MAAT.Domain.Enums;
using SkiaSharp;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 5. Rendu SkiaSharp pur (pas d'export navigateur) :
// géométrie uniquement, aucun texte dans l'image — les libellés de domaine vivent à côté,
// en texte QuestPDF (le tableau de détail de la section 4 les porte déjà), ce qui évite toute
// dépendance à une police à l'intérieur même du rendu SkiaSharp et garde le PNG déterministe.
public static class RadarChartRenderer
{
    // Environ cinq fois la taille d'affichage dans le document (section 5 : au moins trois
    // fois, pour ne pas pixelliser à l'impression) — plus de 400 dpi à la taille affichée,
    // voir QuestPdfReportGeneratorTests.Le_radar_atteint_au_moins_300_dpi_a_la_taille_d_affichage.
    public const int RenderedSizePx = 1500;

    // docs/specs/charte-maat-v2.md, section 3 : couleurs par domaine, à ne jamais réattribuer,
    // identiques à frontend/src/index.css (--color-chart-*) — DomainColorConsistencyTests lit
    // les deux fichiers et l'atteste. internal (pas private) : lu directement par ce test,
    // sans valeur recopiée côté test.
    // Source unique : ReportTheme.DomainHex, partagée avec les pastilles et barres de domaine
    // du document.
    internal static readonly IReadOnlyDictionary<RseDomain, SKColor> DomainColors =
        ReportTheme.DomainHex.ToDictionary(pair => pair.Key, pair => SKColor.Parse(pair.Value));

    private static readonly SKColor GridColor = SKColor.Parse("#E5E7EB"); // --color-border (charte-maat)
    private static readonly SKColor AxisColor = SKColor.Parse("#CBD5E1"); // --color-border-strong (charte-maat)
    private static readonly SKColor BandColor = SKColor.Parse("#F8F9FC"); // --maat-bg (charte-maat)
    private static readonly SKColor SeriesColor = SKColor.Parse("#1565FF"); // --color-blue-maat (charte-maat)

    // Échelle fixe 0-100, jamais adaptée aux données (dashboard.md, section 3) — un domaine
    // absent de scoreByDomain vaut 0, jamais une exception : ce renderer ne connaît que des
    // scores déjà validés en amont par DiagnosticService.
    public static byte[] Render(IReadOnlyDictionary<RseDomain, decimal> scoreByDomain)
    {
        const int size = RenderedSizePx;
        const float padding = size * 0.04f;
        var center = new SKPoint(size / 2f, size / 2f);
        var radius = size / 2f - padding;

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var axisPoints = RadarAxisLayout.Compute();
        var axisDirections = axisPoints.Select(p => new SKPoint(p.DirectionX, p.DirectionY)).ToArray();

        // Fond alterné un anneau sur deux (0-20, 40-60, 80-100) : repère de lecture des
        // paliers sans surcharger la grille de traits.
        using (var bandPaint = new SKPaint { Color = BandColor, Style = SKPaintStyle.Fill, IsAntialias = true })
        using (var whitePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true })
        {
            for (var step = 5; step >= 1; step--)
            {
                DrawPolygon(canvas, step % 2 == 1 ? bandPaint : whitePaint, center, axisDirections, radius * (step / 5f));
            }
        }

        using (var gridPaint = new SKPaint
        {
            Color = GridColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.002f,
            IsAntialias = true,
        })
        {
            for (var step = 1; step <= 4; step++)
            {
                DrawPolygon(canvas, gridPaint, center, axisDirections, radius * (step / 5f));
            }
        }

        using (var axisPaint = new SKPaint
        {
            Color = AxisColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.0025f,
            IsAntialias = true,
        })
        {
            DrawPolygon(canvas, axisPaint, center, axisDirections, radius);
            foreach (var direction in axisDirections)
            {
                canvas.DrawLine(center, new SKPoint(center.X + (direction.X * radius), center.Y + (direction.Y * radius)), axisPaint);
            }
        }

        var scorePoints = new SKPoint[axisPoints.Count];
        for (var i = 0; i < axisPoints.Count; i++)
        {
            var score = scoreByDomain.TryGetValue(axisPoints[i].Domain, out var value) ? value : 0m;
            var ratio = (float)(Math.Clamp(score, 0m, 100m) / 100m);
            scorePoints[i] = new SKPoint(
                center.X + (axisDirections[i].X * radius * ratio),
                center.Y + (axisDirections[i].Y * radius * ratio));
        }

        using (var fillPaint = new SKPaint { Color = SeriesColor.WithAlpha(46), Style = SKPaintStyle.Fill, IsAntialias = true })
        using (var strokePaint = new SKPaint
        {
            Color = SeriesColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.005f,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        })
        {
            var pathBuilder = new SKPathBuilder();
            pathBuilder.AddPoly(scorePoints, close: true);
            using var path = pathBuilder.Detach();
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);
        }

        // Point de chaque domaine à sa couleur, cerclé de blanc pour rester lisible sur le
        // tracé bleu qu'il chevauche.
        using var ringPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        for (var i = 0; i < axisPoints.Count; i++)
        {
            using var dotPaint = new SKPaint { Color = DomainColors[axisPoints[i].Domain], Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(scorePoints[i], size * 0.019f, ringPaint);
            canvas.DrawCircle(scorePoints[i], size * 0.013f, dotPaint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawPolygon(SKCanvas canvas, SKPaint paint, SKPoint center, SKPoint[] axisDirections, float radius)
    {
        var pathBuilder = new SKPathBuilder();
        for (var i = 0; i < axisDirections.Length; i++)
        {
            var point = new SKPoint(center.X + (axisDirections[i].X * radius), center.Y + (axisDirections[i].Y * radius));
            if (i == 0)
            {
                pathBuilder.MoveTo(point);
            }
            else
            {
                pathBuilder.LineTo(point);
            }
        }

        pathBuilder.Close();
        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, paint);
    }
}
