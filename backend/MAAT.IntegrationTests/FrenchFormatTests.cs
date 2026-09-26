using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// Mise en forme française du rapport, construite sans CultureInfo("fr-FR") (voir FrenchFormat) :
// ces tests figent le résultat attendu, qui ne doit dépendre ni de la culture de la machine de
// build ni des données ICU du conteneur de production.
public class FrenchFormatTests
{
    private const char Nnbsp = ' ';
    private const char Nbsp = ' ';

    [Theory]
    [InlineData(40, "40")]
    [InlineData(12.5, "12,5")]
    [InlineData(3.636, "3,64")]
    [InlineData(412000, "412 000")]
    [InlineData(18400000, "18 400 000")]
    public void Number_utilise_la_virgule_et_l_espace_fine(double value, string expected)
    {
        Assert.Equal(expected, FrenchFormat.Number((decimal)value));
    }

    [Fact]
    public void Number_utilise_le_vrai_signe_moins()
    {
        Assert.Equal("−2,5", FrenchFormat.Number(-2.5m, 1));
    }

    [Theory]
    [InlineData(50.19, "50")]
    [InlineData(49.5, "50")]
    [InlineData(49.49, "49")]
    public void Score_arrondit_comme_ScoringService(double value, string expected)
    {
        Assert.Equal(expected, FrenchFormat.Score((decimal)value));
    }

    [Fact]
    public void Percentage_separe_le_symbole_par_une_espace_insecable()
    {
        Assert.Equal($"30{Nbsp}%", FrenchFormat.Percentage(0.30m));
    }

    [Theory]
    [InlineData(4, "+4")]
    [InlineData(-3, "−3")]
    [InlineData(0, "0")]
    public void SignedPoints_affiche_le_sens_de_l_ecart(int delta, string expected)
    {
        Assert.Equal(expected, FrenchFormat.SignedPoints(delta));
    }

    [Fact]
    public void Dates_en_toutes_lettres_et_abregees()
    {
        var date = new DateTimeOffset(2026, 9, 21, 14, 0, 0, TimeSpan.Zero);

        Assert.Equal($"21{Nbsp}septembre 2026", FrenchFormat.LongDate(date));
        Assert.Equal("sept. 2026", FrenchFormat.MonthYear(date));
        Assert.Equal("21/09/2026", FrenchFormat.ShortDate(date));
    }

    [Fact]
    public void Separateur_de_milliers_est_l_espace_fine_insecable()
    {
        Assert.Contains(Nnbsp, FrenchFormat.Number(1000m));
    }
}
