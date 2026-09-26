using System.Globalization;
using System.Text;

namespace MAAT.Infrastructure.Pdf;

// Graphiques vectoriels du rapport (jauge du score global, courbe d'évolution), produits en
// SVG et rendus par QuestPDF sans passer par une image matricielle : nets à toutes les
// échelles d'impression. Géométrie uniquement, aucun texte dans le SVG — les libellés sont
// posés par QuestPDF, avec les polices embarquées (rapport-pdf.md, section 5, et même règle
// que le radar, cas 21). Chaînes construites avec la culture invariante : un « 12,5 » dans un
// attribut SVG serait illisible, et la sortie doit être identique d'une machine à l'autre
// (déterminisme, section 3).
public static class ReportCharts
{
    // Anneau de progression : piste grise complète, arc coloré proportionnel au score, départ
    // à midi, sens horaire (même lecture que ScoreRing.tsx côté tableau de bord).
    public static string ScoreGauge(decimal score, string arcHex, string trackHex, float size, float thickness)
    {
        var ratio = (double)Math.Clamp(score, 0m, 100m) / 100d;
        var radius = (size - thickness) / 2d;
        var center = size / 2d;

        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {size} {size}\">"));
        svg.Append(Invariant($"<circle cx=\"{center}\" cy=\"{center}\" r=\"{radius}\" fill=\"none\" stroke=\"{trackHex}\" stroke-width=\"{thickness}\"/>"));

        if (ratio >= 0.9999)
        {
            svg.Append(Invariant($"<circle cx=\"{center}\" cy=\"{center}\" r=\"{radius}\" fill=\"none\" stroke=\"{arcHex}\" stroke-width=\"{thickness}\"/>"));
        }
        else if (ratio > 0)
        {
            var angle = (2 * Math.PI * ratio) - (Math.PI / 2);
            var endX = center + (radius * Math.Cos(angle));
            var endY = center + (radius * Math.Sin(angle));
            var largeArc = ratio > 0.5 ? 1 : 0;
            svg.Append(Invariant(
                $"<path d=\"M {center} {center - radius} A {radius} {radius} 0 {largeArc} 1 {Round(endX)} {Round(endY)}\" fill=\"none\" stroke=\"{arcHex}\" stroke-width=\"{thickness}\" stroke-linecap=\"round\"/>"));
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    // Courbe du score global dans le temps, échelle fixe 0-100 (même règle que le radar :
    // une échelle adaptée aux données grossirait artificiellement la moindre variation). Les
    // points sont centrés dans N colonnes de même largeur, pour que les libellés de date posés
    // dessous par QuestPDF (une RelativeItem par point) tombent exactement à leur aplomb.
    public static string EvolutionLine(IReadOnlyList<decimal> scores, float width, float height, string lineHex, string gridHex)
    {
        var slot = width / scores.Count;

        double X(int i) => (i + 0.5) * slot;
        double Y(decimal score) => EvolutionTickY(score, height);

        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">"));

        for (var tick = 0; tick <= 100; tick += 25)
        {
            var y = Round(Y(tick));
            var dash = tick == 0 ? string.Empty : " stroke-dasharray=\"3 3\"";
            svg.Append(Invariant($"<line x1=\"0\" y1=\"{y}\" x2=\"{width}\" y2=\"{y}\" stroke=\"{gridHex}\" stroke-width=\"0.75\"{dash}/>"));
        }

        var points = scores.Select((s, i) => (X: Round(X(i)), Y: Round(Y(s)))).ToList();

        if (points.Count > 1)
        {
            var baseline = Round(Y(0));
            var area = new StringBuilder(Invariant($"M {points[0].X} {baseline}"));
            foreach (var (x, y) in points)
            {
                area.Append(Invariant($" L {x} {y}"));
            }

            area.Append(Invariant($" L {points[^1].X} {baseline} Z"));
            svg.Append(Invariant($"<path d=\"{area}\" fill=\"{lineHex}\" fill-opacity=\"0.08\"/>"));

            var line = string.Join(" ", points.Select(p => Invariant($"{p.X},{p.Y}")));
            svg.Append(Invariant($"<polyline points=\"{line}\" fill=\"none\" stroke=\"{lineHex}\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>"));
        }

        for (var i = 0; i < points.Count; i++)
        {
            var isLast = i == points.Count - 1;
            var fill = isLast ? lineHex : "#FFFFFF";
            svg.Append(Invariant($"<circle cx=\"{points[i].X}\" cy=\"{points[i].Y}\" r=\"{(isLast ? 4.5 : 3.5)}\" fill=\"{fill}\" stroke=\"{lineHex}\" stroke-width=\"2\"/>"));
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    // Ordonnée d'un score dans la courbe d'évolution, partagée avec les graduations posées
    // en texte par QuestPDF à gauche du graphique : les deux doivent tomber sur la même ligne.
    public static float EvolutionTickY(decimal score, float height)
    {
        const float padY = 8f;
        return padY + ((height - (2 * padY)) * (1 - ((float)Math.Clamp(score, 0m, 100m) / 100f)));
    }

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
