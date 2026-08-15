using MAAT.Domain.Enums;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 5, cas 21 : les cinq libellés d'axe posés par QuestPDF
// autour de l'image (QuestPdfReportGenerator) doivent utiliser exactement le même ordre et la
// même direction que les axes dessinés par RadarChartRenderer (SkiaSharp) — sans quoi un
// libellé se retrouverait à côté d'un axe qui n'est pas le sien. Cette classe est la source
// unique des deux, pure et testable indépendamment du rendu (SkiaSharp ou QuestPDF).
public static class RadarAxisLayout
{
    // Ordre fixe des axes (dashboard.md, section 3 : « même ordre des axes » que le tableau
    // de bord) — celui de l'énumération RseDomain, aligné sur modele-donnees.md.
    public static readonly IReadOnlyList<RseDomain> AxisOrder =
    [
        RseDomain.Environmental,
        RseDomain.Social,
        RseDomain.Ethics,
        RseDomain.Procurement,
        RseDomain.Governance,
    ];

    // DirectionX/DirectionY : vecteur unitaire, à multiplier par un rayon en pixels (SkiaSharp)
    // ou en points (QuestPDF) selon l'appelant. Convention écran (Y croissant vers le bas),
    // partagée par les deux rendus.
    public readonly record struct AxisPoint(RseDomain Domain, float DirectionX, float DirectionY);

    public static IReadOnlyList<AxisPoint> Compute()
    {
        var points = new AxisPoint[AxisOrder.Count];
        for (var i = 0; i < AxisOrder.Count; i++)
        {
            // Premier axe vers le haut, sens horaire — même convention que RadarChart de
            // Recharts côté frontend (DomainRadarChart.tsx), pour un ordre des axes identique.
            var angle = (-Math.PI / 2) + (i * (2 * Math.PI / AxisOrder.Count));
            points[i] = new AxisPoint(AxisOrder[i], (float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        return points;
    }
}
