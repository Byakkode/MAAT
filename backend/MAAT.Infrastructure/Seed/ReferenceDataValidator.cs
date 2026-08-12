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
    // docs/specs/modele-donnees.md, section « Fichiers et colonnes » : source de vérité de
    // ces trois listes. L'ordre n'a pas besoin de correspondre — CsvFile lit les champs par
    // nom, jamais par position — mais l'ensemble, si.
    private static readonly IReadOnlyList<string> QuestionsExpectedHeader =
        ["code", "domain", "text", "help_text", "weight", "display_order", "vsme_ref", "iso_ref", "gri_ref", "ecovadis_ref", "is_active"];

    private static readonly IReadOnlyList<string> RecommendationsExpectedHeader =
        ["code", "domain", "action_text", "detail_text", "impact_points", "effort_level", "trigger_question_code", "trigger_max_value", "is_active"];

    private static readonly IReadOnlyList<string> SectorWeightsExpectedHeader = ["sector_code", "domain", "weight"];

    public static ReferenceDataValidationResult Validate() => Validate(ReferenceDataRows.ReadCsv);

    // Point d'entrée testable : accepte n'importe quelle source de contenu CSV — les
    // fichiers de seed réels embarqués par défaut (Validate() ci-dessus, utilisé par
    // Program.cs), un contenu fabriqué en test (voir ReferenceDataValidatorTests.cs). Le
    // paramètre expose CsvFileContent (internal) : cette surcharge l'est donc aussi.
    internal static ReferenceDataValidationResult Validate(Func<string, CsvFileContent> readCsv)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var questions = ReadAndParseAll(
            "questions.csv", readCsv, QuestionsExpectedHeader, errors, ReferenceDataRows.ParseQuestion);
        var recommendations = ReadAndParseAll(
            "recommendations.csv", readCsv, RecommendationsExpectedHeader, errors, ReferenceDataRows.ParseRecommendation);
        var sectorWeights = ReadAndParseAll(
            "sector-weights.csv", readCsv, SectorWeightsExpectedHeader, errors, ReferenceDataRows.ParseSectorWeight);

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
        string fileName,
        Func<string, CsvFileContent> readCsv,
        IReadOnlyList<string> expectedHeader,
        List<string> errors,
        Func<string, CsvRow, T> parse)
    {
        CsvFileContent file;
        try
        {
            file = readCsv(fileName);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
            return null;
        }

        // Un en-tête qui ne correspond pas aux colonnes attendues n'est pas détecté ligne
        // par ligne : une colonne facultative absente (OrNull) se lit comme "présente mais
        // vide", jamais comme une erreur — c'est passé inaperçu pour effort_level sur
        // recommendations.csv avant d'être repéré en relecture manuelle. Si l'en-tête est
        // faux, l'analyse ligne par ligne est sautée : elle ne ferait que répéter la même
        // erreur de colonne manquante sur chacune des dizaines de lignes du fichier.
        if (!CheckHeader(fileName, file.Header, expectedHeader, errors))
        {
            return null;
        }

        var parsed = new List<T>();
        foreach (var row in file.Rows)
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

    // Retourne false si l'en-tête est invalide (colonne manquante, inattendue ou dupliquée)
    // — l'appelant saute alors l'analyse ligne par ligne pour ce fichier.
    private static bool CheckHeader(string fileName, IReadOnlyList<string> header, IReadOnlyList<string> expected, List<string> errors)
    {
        var valid = true;

        var duplicates = header.GroupBy(c => c, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            errors.Add($"{fileName} : colonne(s) dupliquée(s) dans l'en-tête — {string.Join(", ", duplicates)}.");
            valid = false;
        }

        var headerSet = header.ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(c => !headerSet.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            errors.Add($"{fileName} : colonne(s) manquante(s) dans l'en-tête — {string.Join(", ", missing)}.");
            valid = false;
        }

        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var unexpected = header.Where(c => !expectedSet.Contains(c)).ToList();
        if (unexpected.Count > 0)
        {
            errors.Add($"{fileName} : colonne(s) inattendue(s) dans l'en-tête — {string.Join(", ", unexpected)}.");
            valid = false;
        }

        return valid;
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
