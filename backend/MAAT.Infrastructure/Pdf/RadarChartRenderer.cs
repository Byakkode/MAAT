using MAAT.Domain.Enums;
using SkiaSharp;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 5. Rendu SkiaSharp pur (pas d'export navigateur) :
// géométrie uniquement, aucun texte dans l'image — les libellés de domaine vivent à côté,
// en texte QuestPDF (le tableau de détail de la section 4 les porte déjà), ce qui évite toute
// dépendance à une police à l'intérieur même du rendu SkiaSharp et garde le PNG déterministe.
public static class RadarChartRenderer
{
    // ~3x une taille d'affichage cible d'environ 400 px dans le document (section 5 : dessiner
    // à environ trois fois la taille d'affichage cible, pour ne pas pixelliser à l'impression).
    public const int RenderedSizePx = 1200;

    // docs/specs/dashboard.md, section 3 : couleurs par domaine, à ne jamais réattribuer.
    private static readonly IReadOnlyDictionary<RseDomain, SKColor> DomainColors = new Dictionary<RseDomain, SKColor>
    {
        [RseDomain.Environmental] = SKColor.Parse("#1B9E5F"),
        [RseDomain.Social] = SKColor.Parse("#1E88E5"),
        [RseDomain.Ethics] = SKColor.Parse("#7E57C2"),
        [RseDomain.Procurement] = SKColor.Parse("#E08A1E"),
        [RseDomain.Governance] = SKColor.Parse("#4A5568"),
    };

    private static readonly SKColor GridColor = SKColor.Parse("#E5E7EB"); // --color-border (charte-maat)
    private static readonly SKColor SeriesColor = SKColor.Parse("#1565FF"); // --color-blue-maat (charte-maat)

    // Échelle fixe 0-100, jamais adaptée aux données (dashboard.md, section 3) — un domaine
    // absent de scoreByDomain vaut 0, jamais une exception : ce renderer ne connaît que des
    // scores déjà validés en amont par DiagnosticService.
    public static byte[] Render(IReadOnlyDictionary<RseDomain, decimal> scoreByDomain)
    {
        const int size = RenderedSizePx;
        const float padding = size * 0.12f;
        var center = new SKPoint(size / 2f, size / 2f);
        var radius = size / 2f - padding;

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var axisPoints = RadarAxisLayout.Compute();
        var axisDirections = axisPoints.Select(p => new SKPoint(p.DirectionX, p.DirectionY)).ToArray();

        using (var gridPaint = new SKPaint
        {
            Color = GridColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.0025f,
            IsAntialias = true,
        })
        {
            for (var step = 1; step <= 5; step++)
            {
                DrawPolygon(canvas, gridPaint, center, axisDirections, radius * (step / 5f));
            }

            foreach (var direction in axisDirections)
            {
                canvas.DrawLine(center, new SKPoint(center.X + (direction.X * radius), center.Y + (direction.Y * radius)), gridPaint);
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

        using (var fillPaint = new SKPaint { Color = SeriesColor.WithAlpha(64), Style = SKPaintStyle.Fill, IsAntialias = true })
        using (var strokePaint = new SKPaint { Color = SeriesColor, Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.004f, IsAntialias = true })
        {
            var pathBuilder = new SKPathBuilder();
            pathBuilder.AddPoly(scorePoints, close: true);
            using var path = pathBuilder.Detach();
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);
        }

        for (var i = 0; i < axisPoints.Count; i++)
        {
            using var dotPaint = new SKPaint { Color = DomainColors[axisPoints[i].Domain], Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawCircle(scorePoints[i], size * 0.012f, dotPaint);
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
