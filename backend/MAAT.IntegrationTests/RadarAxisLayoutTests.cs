using MAAT.Domain.Enums;
using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, section 5, cas 21 : source unique de la géométrie des axes,
// partagée par RadarChartRenderer (SkiaSharp) et QuestPdfReportGenerator (libellés QuestPDF).
// Test pur, aucune base de données — comme CsvFileTests.cs pour la même raison.
public class RadarAxisLayoutTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void Compute_retourne_cinq_axes_dans_l_ordre_de_RseDomain()
    {
        var points = RadarAxisLayout.Compute();

        Assert.Equal(5, points.Count);
        Assert.Equal(
            [RseDomain.Environmental, RseDomain.Social, RseDomain.Ethics, RseDomain.Procurement, RseDomain.Governance],
            points.Select(p => p.Domain));
    }

    [Fact]
    public void Premier_axe_pointe_vers_le_haut()
    {
        var points = RadarAxisLayout.Compute();

        Assert.Equal(0f, points[0].DirectionX, Tolerance);
        Assert.Equal(-1f, points[0].DirectionY, Tolerance);
    }

    [Fact]
    public void Les_cinq_axes_sont_espaces_de_72_degres_dans_le_sens_horaire()
    {
        var points = RadarAxisLayout.Compute();

        for (var i = 0; i < points.Count; i++)
        {
            var expectedAngle = (-Math.PI / 2) + (i * (2 * Math.PI / 5));
            Assert.Equal((float)Math.Cos(expectedAngle), points[i].DirectionX, Tolerance);
            Assert.Equal((float)Math.Sin(expectedAngle), points[i].DirectionY, Tolerance);
        }
    }

    [Fact]
    public void Chaque_direction_est_un_vecteur_unitaire()
    {
        var points = RadarAxisLayout.Compute();

        foreach (var point in points)
        {
            var magnitude = Math.Sqrt((point.DirectionX * point.DirectionX) + (point.DirectionY * point.DirectionY));
            Assert.Equal(1.0, magnitude, 0.0001);
        }
    }
}
