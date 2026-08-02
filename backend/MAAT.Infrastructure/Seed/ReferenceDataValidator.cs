using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Infrastructure.Seed;

public sealed record ReferenceDataValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

// docs/specs/modele-donnees.md, section « Seed des données de référence » : la commande
// "seed --validate", destinée au rédacteur de contenu pour se relire — jamais de base de
// données requise, jamais d'écriture. Relit les trois CSV et collecte le plus d'anomalies
// possible en un seul passage plutôt que de s'arrêter à la première : structure et types
// par ligne, doublons de code, couverture et somme des pondérations sectorielles, et enfin
// les avertissements de calibrage du cas 23 de docs/specs/recommandations.md (jamais
// bloquants, comme la spec l'exige).
public static class ReferenceDataValidator
{
    public static ReferenceDataValidationResult Validate() => Validate(ReferenceDataRows.ReadCsv);

    // Point d'entrée testable : accepte n'importe quelle source de lignes CSV — les
    // fichiers de seed réels embarqués par défaut (Validate() ci-dessus, utilisé par
    // Program.cs), un contenu fabriqué en test (voir ReferenceDataValidatorTests.cs). Le
    // paramètre expose CsvRow (internal) : cette surcharge l'est donc aussi.
    internal static ReferenceDataValidationResult Validate(Func<string, IReadOnlyList<CsvRow>> readCsv)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var questions = ReadAndParseAll("questions.csv", readCsv, errors, ReferenceDataRows.ParseQuestion);
        var recommendations = ReadAndParseAll("recommendations.csv", readCsv, errors, ReferenceDataRows.ParseRecommendation);
        var sectorWeights = ReadAndParseAll("sector-weights.csv", readCsv, errors, ReferenceDataRows.ParseSectorWeight);

        if (questions is not null)
        {
            CheckDuplicateCodes("questions.csv", questions.Select(q => q.Code), errors);
        }

        if (recommendations is not null)
        {
            CheckDuplicateCodes("recommendations.csv", recommendations.Select(r => r.Code), errors);
        }

        if (sectorWeights is not null)
        {
            CheckSectorWeightCoverage(sectorWeights, errors);
        }

        // Le contrôle de calibration construit de vraies entités Question/Recommendation
        // (jamais persistées) : ne s'exécute que si tout le reste est déjà propre, pour ne
        // jamais tenter de construire une entité depuis des données qu'on sait invalides.
        if (questions is not null && recommendations is not null && errors.Count == 0)
        {
            CheckCalibration(questions, recommendations, warnings);
        }

        return new ReferenceDataValidationResult(errors, warnings);
    }

    private static IReadOnlyList<T>? ReadAndParseAll<T>(
        string fileName, Func<string, IReadOnlyList<CsvRow>> readCsv, List<string> errors, Func<string, CsvRow, T> parse)
    {
        IReadOnlyList<CsvRow> rows;
        try
        {
            rows = readCsv(fileName);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
            return null;
        }

        var parsed = new List<T>();
        foreach (var row in rows)
        {
            try
            {
                parsed.Add(parse(fileName, row));
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }
        }

        return parsed;
    }

    private static void CheckDuplicateCodes(string fileName, IEnumerable<string> codes, List<string> errors)
    {
        foreach (var group in codes.GroupBy(c => c, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            errors.Add($"{fileName} : code en doublon « {group.Key} » ({group.Count()} occurrences).");
        }
    }

    private static void CheckSectorWeightCoverage(IReadOnlyList<ParsedSectorWeight> rows, List<string> errors)
    {
        var allDomains = Enum.GetValues<RseDomain>();

        foreach (var sector in rows.GroupBy(r => r.SectorCode))
        {
            var label = sector.Key ?? "défaut (sector_code vide)";
            var byDomain = sector.GroupBy(r => r.Domain).ToList();

            var duplicateDomains = byDomain.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateDomains.Count > 0)
            {
                errors.Add($"sector-weights.csv, secteur {label} : domaine(s) en double — {string.Join(", ", duplicateDomains)}.");
                continue;
            }

            var missingDomains = allDomains.Except(byDomain.Select(g => g.Key)).ToList();
            if (missingDomains.Count > 0)
            {
                errors.Add($"sector-weights.csv, secteur {label} : domaine(s) manquant(s) — {string.Join(", ", missingDomains)}.");
                continue;
            }

            var sum = sector.Sum(r => r.Weight);
            if (sum != 1m)
            {
                errors.Add($"sector-weights.csv, secteur {label} : somme des poids = {sum}, attendu 1.");
            }
        }
    }

    private static void CheckCalibration(
        IReadOnlyList<ParsedQuestion> questions, IReadOnlyList<ParsedRecommendation> recommendations, List<string> warnings)
    {
        var activeQuestions = questions
            .Where(q => q.IsActive)
            .Select(q => new Question(q.Code, q.Text, q.Domain, q.Weight, q.DisplayOrder))
            .ToList();

        var activeRecommendations = recommendations
            .Where(r => r.IsActive)
            .Select(r => new Recommendation(r.Code, r.Domain, r.ActionText, r.ImpactPoints, r.EffortLevel, r.TriggerQuestionCode, r.TriggerMaxValue))
            .ToList();

        foreach (var warning in RecommendationCalibrationChecker.FindMiscalibrated(activeRecommendations, activeQuestions))
        {
            warnings.Add(
                $"recommendations.csv : {warning.RecommendationCode} — impact_points {warning.ImpactPoints} dépasse le gain "
                    + $"maximal théorique ({warning.MaxTheoreticalGain:0.##}) de sa question déclencheuse.");
        }
    }
}
