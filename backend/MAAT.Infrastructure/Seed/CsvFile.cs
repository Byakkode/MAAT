using System.Text;

namespace MAAT.Infrastructure.Seed;

// Une ligne de données CSV et le numéro de la ligne physique où elle commence dans le
// fichier (1 = l'en-tête) — permet des messages d'erreur exploitables par un rédacteur qui
// n'écrit pas de code (voir ReferenceDataValidator).
internal sealed record CsvRow(int LineNumber, IReadOnlyDictionary<string, string> Fields);

// Header exposé séparément des lignes : ReferenceDataRows ne lit les champs que par nom
// (jamais par position), donc un en-tête qui ne correspond pas aux colonnes attendues de
// modele-donnees.md passe inaperçu ligne par ligne — une colonne facultative absente
// (OrNull) se lit comme "présente mais vide", pas comme une erreur. Voir
// ReferenceDataValidator.CheckHeader, le contrôle qui compare cet en-tête à la liste
// attendue avant toute analyse ligne par ligne.
internal sealed record CsvFileContent(IReadOnlyList<string> Header, IReadOnlyList<CsvRow> Rows);

// Analyseur CSV endurci face aux exports réels de tableur (Excel « CSV UTF-8 »,
// LibreOffice Calc, Google Sheets) — voir docs/specs/modele-donnees.md :
// - BOM UTF-8 en tête de fichier : détecté et retiré.
// - Fins de ligne CRLF, LF ou CR seul (ancien Mac) : acceptées indifféremment, y compris
//   mélangées dans un même fichier.
// - Retour à la ligne à l'intérieur d'un champ entre guillemets doubles : le champ reste
//   une seule valeur logique même s'il s'étend sur plusieurs lignes physiques du fichier.
// - Guillemet doublé ("") à l'intérieur d'un champ entre guillemets : échappement standard
//   RFC 4180.
// - Export accidentel séparé par point-virgule (réglage régional Excel courant) : détecté
//   sur la première ligne et rejeté avec un message explicite plutôt que des erreurs de
//   colonnes incompréhensibles plus loin dans le fichier.
// - Nombre de colonnes incorrect sur une ligne : rejeté avec le numéro de la ligne fautive.
// - Lignes de commentaire ('#' en tout premier caractère) avant l'en-tête : ignorées — sert
//   à signaler qu'un fichier de seed est provisoire (voir Seed/demo/). Seulement en tête de
//   fichier, jamais mêlées aux données, pour ne pas complexifier le tokenizer ci-dessous.
//
// Ne dépend d'aucun paquet supplémentaire : suffisant pour les fichiers de ce dossier, dont
// le contenu est entièrement sous contrôle de l'équipe.
internal static class CsvFile
{
    public static CsvFileContent ReadRows(Stream stream, string fileName)
    {
        var content = ReadContentWithoutBom(stream);
        if (content.Length == 0)
        {
            throw new InvalidOperationException($"{fileName} : fichier vide.");
        }

        var (body, headerLine) = SkipLeadingComments(content);
        if (body.Length == 0)
        {
            throw new InvalidOperationException($"{fileName} : ligne d'en-tête manquante.");
        }

        RequireNotSemicolonSeparated(fileName, FirstLine(body));

        var records = Tokenize(body, headerLine).Where(r => !IsBlank(r.Fields)).ToList();
        if (records.Count == 0)
        {
            throw new InvalidOperationException($"{fileName} : ligne d'en-tête manquante.");
        }

        var header = records[0].Fields;
        var rows = new List<CsvRow>();

        for (var i = 1; i < records.Count; i++)
        {
            var (lineNumber, fields) = records[i];
            if (fields.Count != header.Count)
            {
                throw new InvalidOperationException(
                    $"{fileName}, ligne {lineNumber} : {header.Count} colonne(s) attendue(s), {fields.Count} trouvée(s).");
            }

            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < header.Count; c++)
            {
                row[header[c]] = fields[c];
            }

            rows.Add(new CsvRow(lineNumber, row));
        }

        return new CsvFileContent(header, rows);
    }

    private static string ReadContentWithoutBom(Stream stream)
    {
        // detectEncodingFromByteOrderMarks retire déjà un BOM UTF-8 détecté en tête de
        // flux, mais un U+FEFF résiduel en tout début de contenu (une source déjà décodée
        // en amont, par exemple) casserait silencieusement le nom de la première colonne
        // de l'en-tête — d'où la vérification explicite ci-dessous en défense en profondeur.
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        return content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
    }

    private static bool IsBlank(IReadOnlyList<string> fields) => fields.Count == 1 && fields[0].Length == 0;

    // Retire les lignes de commentaire en tête de fichier et retourne, avec le contenu
    // restant, le numéro de ligne physique où celui-ci reprend — pour que CsvRow.LineNumber
    // continue de désigner la vraie ligne du fichier sur disque malgré les lignes retirées.
    private static (string Body, int StartLine) SkipLeadingComments(string content)
    {
        var line = 1;
        var i = 0;

        while (i < content.Length && content[i] == '#')
        {
            var end = content.IndexOfAny(['\r', '\n'], i);
            if (end < 0)
            {
                return (string.Empty, line);
            }

            i = content[end] == '\r' && end + 1 < content.Length && content[end + 1] == '\n' ? end + 2 : end + 1;
            line++;
        }

        return (content[i..], line);
    }

    private static string FirstLine(string content)
    {
        var end = content.IndexOfAny(['\r', '\n']);
        return end < 0 ? content : content[..end];
    }

    // Réglage régional Excel courant : export « CSV » avec point-virgule au lieu de
    // virgule. Comparer un décompte de séparateurs sur la ligne brute, avant tout
    // découpage en colonnes, plutôt que de tenter un parsing tolérant : un message clair
    // sur la cause probable vaut mieux qu'une cascade d'erreurs de colonnes.
    private static void RequireNotSemicolonSeparated(string fileName, string headerLine)
    {
        var semicolons = headerLine.Count(c => c == ';');
        var commas = headerLine.Count(c => c == ',');
        if (semicolons > commas)
        {
            throw new InvalidOperationException(
                $"{fileName} : fichier exporté avec le séparateur point-virgule — réenregistrer en CSV UTF-8 séparé par des virgules.");
        }
    }

    private static List<(int LineNumber, List<string> Fields)> Tokenize(string content, int startLine)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = startLine;
        var recordStartLine = startLine;
        var i = 0;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            records.Add((recordStartLine, fields));
            fields = [];
        }

        while (i < content.Length)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    field.Append('\n');
                    line++;
                    i += 2;
                    continue;
                }

                if (c is '\n' or '\r')
                {
                    field.Append('\n');
                    line++;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }

            if (c == ',')
            {
                EndField();
                i++;
                continue;
            }

            if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
            {
                EndRecord();
                line++;
                recordStartLine = line;
                i += 2;
                continue;
            }

            if (c is '\n' or '\r')
            {
                EndRecord();
                line++;
                recordStartLine = line;
                i++;
                continue;
            }

            field.Append(c);
            i++;
        }

        // Dernière ligne sans retour à la ligne final : encore en attente, à clore.
        if (field.Length > 0 || fields.Count > 0)
        {
            EndRecord();
        }

        return records;
    }
}
