using System.Globalization;

namespace MAAT.Infrastructure.Pdf;

// Mise en forme des nombres et des dates du rapport, à la française (virgule décimale,
// espace fine insécable entre les milliers, mois en toutes lettres). Construite à la main
// plutôt qu'avec CultureInfo("fr-FR") : un conteneur Linux minimal peut ne pas embarquer les
// données ICU françaises, et le rapport sortirait alors en format anglais sans erreur ni
// avertissement — même piège que les polices (rapport-pdf.md, section 5).
public static class FrenchFormat
{
    private const char ThousandsSeparator = '\u202F'; // espace fine insécable
    private const char NonBreakingSpace = '\u00A0';
    private const string MinusSign = "\u2212";

    private static readonly NumberFormatInfo French = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ThousandsSeparator.ToString(),
        NumberGroupSizes = [3],
        NegativeSign = MinusSign,
    };

    private static readonly string[] Months =
    [
        "janvier", "février", "mars", "avril", "mai", "juin",
        "juillet", "août", "septembre", "octobre", "novembre", "décembre",
    ];

    // Abréviations usuelles (Imprimerie nationale).
    private static readonly string[] ShortMonths =
    [
        "janv.", "févr.", "mars", "avr.", "mai", "juin",
        "juil.", "août", "sept.", "oct.", "nov.", "déc.",
    ];

    // Identique à ScoringService.RoundForDisplay (MAAT.Domain) : arrondi au plus proche, .5
    // vers le haut — le score affiché dans le document doit être celui de l'écran (cas 13).
    public static int Round(decimal value) => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static string Score(decimal value) => Round(value).ToString(CultureInfo.InvariantCulture);

    public static string Percentage(decimal ratio) => $"{Round(ratio * 100)}{NonBreakingSpace}%";

    // Au plus `decimals` décimales, zéros inutiles retirés : « 40 », « 12,5 », « 3,64 ».
    public static string Number(decimal value, int decimals = 2) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("#,0." + new string('#', decimals), French);

    public static string Number(double value, int decimals = 2) => Number((decimal)value, decimals);

    // Écart signé en points : « +4 », « −3 », « 0 ».
    public static string SignedPoints(int delta) => delta switch
    {
        > 0 => $"+{delta}",
        < 0 => $"{MinusSign}{-delta}",
        _ => "0",
    };

    public static string Unit(string value, string unit) => $"{value}{NonBreakingSpace}{unit}";

    // « 21 septembre 2026 »
    public static string LongDate(DateTimeOffset value) =>
        $"{value.Day.ToString(CultureInfo.InvariantCulture)}{NonBreakingSpace}{Months[value.Month - 1]} {value.Year.ToString(CultureInfo.InvariantCulture)}";

    // « 21/09/2026 »
    public static string ShortDate(DateTimeOffset value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // « sept. 2026 » : axe de la courbe d'évolution, où la date longue ne tient pas.
    public static string MonthYear(DateTimeOffset value) =>
        $"{ShortMonths[value.Month - 1]} {value.Year.ToString(CultureInfo.InvariantCulture)}";
}
