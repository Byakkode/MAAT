using System.Globalization;
using System.Reflection;
using MAAT.Domain.Enums;

namespace MAAT.Infrastructure.Seed;

internal sealed record ParsedQuestion(
    string Code,
    RseDomain Domain,
    string Text,
    string? HelpText,
    decimal Weight,
    int DisplayOrder,
    string? VsmeRef,
    string? IsoRef,
    string? GriRef,
    string? EcovadisRef,
    bool IsActive);

internal sealed record ParsedRecommendation(
    string Code,
    RseDomain Domain,
    string ActionText,
    string? DetailText,
    decimal ImpactPoints,
    EffortLevel EffortLevel,
    string TriggerQuestionCode,
    int TriggerMaxValue,
    bool IsActive);

internal sealed record ParsedSectorWeight(string? SectorCode, RseDomain Domain, decimal Weight);

// Lecture et analyse par ligne des trois fichiers CSV — partagées entre ReferenceDataSeeder
// (qui laisse toute exception remonter : une ligne invalide doit bloquer le seed) et
// ReferenceDataValidator (qui capture ces mêmes exceptions pour construire un rapport
// listant tous les problèmes plutôt que de s'arrêter au premier). Chaque échec porte le
// nom du fichier et le numéro de ligne, jusque dans les contraintes déjà vérifiées par les
// constructeurs des entités du Domain (poids, seuil 0-5) : les anticiper ici évite qu'une
// ArgumentOutOfRangeException sans contexte de fichier ne remonte telle quelle.
internal static class ReferenceDataRows
{
    public static CsvFileContent ReadCsv(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"MAAT.Infrastructure.Seed.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Fichier de seed introuvable : {resourceName}");
        return CsvFile.ReadRows(stream, fileName);
    }

    public static ParsedQuestion ParseQuestion(string fileName, CsvRow row)
    {
        var weight = ParseDecimal(fileName, row, "weight");
        if (weight <= 0)
        {
            throw Invalid(fileName, row, "weight doit être strictement positif.");
        }

        return new ParsedQuestion(
            Require(fileName, row, "code"),
            ParseEnum<RseDomain>(fileName, row, "domain"),
            Require(fileName, row, "text"),
            OrNull(row, "help_text"),
            weight,
            ParseInt(fileName, row, "display_order"),
            OrNull(row, "vsme_ref"),
            OrNull(row, "iso_ref"),
            OrNull(row, "gri_ref"),
            OrNull(row, "ecovadis_ref"),
            ParseBool(fileName, row, "is_active"));
    }

    public static ParsedRecommendation ParseRecommendation(string fileName, CsvRow row)
    {
        var triggerMaxValue = ParseInt(fileName, row, "trigger_max_value");
        if (triggerMaxValue is < 0 or > 5)
        {
            throw Invalid(fileName, row, "trigger_max_value doit être compris entre 0 et 5.");
        }

        return new ParsedRecommendation(
            Require(fileName, row, "code"),
            ParseEnum<RseDomain>(fileName, row, "domain"),
            Require(fileName, row, "action_text"),
            OrNull(row, "detail_text"),
            ParseDecimal(fileName, row, "impact_points"),
            ParseEnum<EffortLevel>(fileName, row, "effort_level"),
            Require(fileName, row, "trigger_question_code"),
            triggerMaxValue,
            ParseBool(fileName, row, "is_active"));
    }

    public static ParsedSectorWeight ParseSectorWeight(string fileName, CsvRow row)
    {
        var weight = ParseDecimal(fileName, row, "weight");
        if (weight < 0 || weight > 1)
        {
            throw Invalid(fileName, row, "weight doit être compris entre 0 et 1.");
        }

        return new ParsedSectorWeight(OrNull(row, "sector_code"), ParseEnum<RseDomain>(fileName, row, "domain"), weight);
    }

    private static InvalidOperationException Invalid(string fileName, CsvRow row, string message) =>
        new($"{fileName}, ligne {row.LineNumber} : {message}");

    private static string Require(string fileName, CsvRow row, string column) =>
        row.Fields.TryGetValue(column, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : throw Invalid(fileName, row, $"colonne '{column}' manquante ou vide.");

    private static string? OrNull(CsvRow row, string column) =>
        row.Fields.TryGetValue(column, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    private static decimal ParseDecimal(string fileName, CsvRow row, string column)
    {
        var raw = Require(fileName, row, column);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Invalid(fileName, row, $"colonne '{column}' : nombre décimal invalide « {raw} » (point, pas virgule).");
    }

    private static int ParseInt(string fileName, CsvRow row, string column)
    {
        var raw = Require(fileName, row, column);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Invalid(fileName, row, $"colonne '{column}' : entier invalide « {raw} ».");
    }

    private static bool ParseBool(string fileName, CsvRow row, string column)
    {
        var raw = Require(fileName, row, column);
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw Invalid(fileName, row, $"colonne '{column}' : valeur booléenne invalide « {raw} » (attendu 'true' ou 'false').");
    }

    private static T ParseEnum<T>(string fileName, CsvRow row, string column) where T : struct, Enum
    {
        var raw = Require(fileName, row, column);
        return Enum.TryParse<T>(raw, out var value)
            ? value
            : throw Invalid(fileName, row, $"colonne '{column}' : valeur '{raw}' invalide pour l'énumération {typeof(T).Name}.");
    }
}
