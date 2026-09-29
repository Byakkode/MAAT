using System.Globalization;
using System.Text.RegularExpressions;
using MAAT.Domain.Knowledge;

namespace MAAT.Infrastructure.Knowledge;

// docs/specs/documentation.md, section 2 : un article est un fichier Markdown précédé d'un
// en-tête entre deux lignes « --- » :
//
//   ---
//   slug: quest-ce-que-la-rse            (identique au nom du fichier, sans .md)
//   title: Qu'est-ce que la RSE ?
//   summary: Une phrase affichée dans la liste et les résultats de recherche.
//   category: GettingStarted             (KnowledgeCategory)
//   level: Essentials                    (Essentials ou Expert)
//   order: 1                             (rang de lecture dans la rubrique, entier positif)
//   tags: définition, ISO 26000          (séparés par des virgules)
//   updated: 2026-09-28                  (date de vérification des sources)
//   source: Titre de la source | https://…   (une ligne par source, au moins une)
//   ---
//
// Lu à la main plutôt qu'avec une bibliothèque YAML : cinq champs plats ne justifient pas une
// dépendance. Strict : un champ manquant, inconnu ou mal formé lève une exception qui nomme
// le fichier — un article cassé fait échouer le démarrage et les tests, il ne disparaît
// jamais en silence de la base.
public static partial class KnowledgeArticleParser
{
    private const string Delimiter = "---";

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    private static readonly HashSet<string> SingleKeys = ["slug", "title", "summary", "category", "level", "order", "tags", "updated"];

    public static KnowledgeArticle Parse(string fileName, string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != Delimiter)
        {
            throw Error(fileName, "l'en-tête doit commencer par une ligne « --- ».");
        }

        var end = Array.FindIndex(lines, 1, line => line.Trim() == Delimiter);
        if (end < 0)
        {
            throw Error(fileName, "l'en-tête n'est pas refermé par une ligne « --- ».");
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = new List<KnowledgeSource>();
        for (var i = 1; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw Error(fileName, $"ligne d'en-tête sans « clé: valeur » : « {line.Trim()} ».");
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            if (key == "source")
            {
                sources.Add(ParseSource(fileName, value));
            }
            else if (!SingleKeys.Contains(key))
            {
                throw Error(fileName, $"champ d'en-tête inconnu : « {key} ».");
            }
            else if (!fields.TryAdd(key, value))
            {
                throw Error(fileName, $"champ d'en-tête en double : « {key} ».");
            }
        }

        var slug = Required(fields, fileName, "slug");
        if (!SlugPattern().IsMatch(slug))
        {
            throw Error(fileName, $"slug invalide « {slug} » : minuscules, chiffres et tirets seulement.");
        }

        if (slug != Path.GetFileNameWithoutExtension(fileName))
        {
            throw Error(fileName, $"le slug « {slug} » doit être identique au nom du fichier.");
        }

        if (!Enum.TryParse<KnowledgeCategory>(Required(fields, fileName, "category"), ignoreCase: false, out var category)
            || !Enum.IsDefined(category))
        {
            throw Error(fileName, $"rubrique inconnue « {fields["category"]} ».");
        }

        if (!Enum.TryParse<KnowledgeLevel>(Required(fields, fileName, "level"), ignoreCase: false, out var level)
            || !Enum.IsDefined(level))
        {
            throw Error(fileName, $"niveau inconnu « {fields["level"]} » : Essentials ou Expert.");
        }

        if (!int.TryParse(Required(fields, fileName, "order"), NumberStyles.None, CultureInfo.InvariantCulture, out var order) || order < 1)
        {
            throw Error(fileName, $"rang de lecture « {fields["order"]} » invalide : entier positif attendu.");
        }

        if (!DateOnly.TryParseExact(Required(fields, fileName, "updated"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var updated))
        {
            throw Error(fileName, $"date « {fields["updated"]} » invalide : format aaaa-mm-jj attendu.");
        }

        if (sources.Count == 0)
        {
            throw Error(fileName, "au moins une source est exigée (ligne « source: Titre | https://… »).");
        }

        var tags = (fields.GetValueOrDefault("tags") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var body = string.Join('\n', lines[(end + 1)..]).Trim();
        if (body.Length == 0)
        {
            throw Error(fileName, "l'article n'a pas de contenu après l'en-tête.");
        }

        return new KnowledgeArticle(
            slug,
            Required(fields, fileName, "title"),
            Required(fields, fileName, "summary"),
            category,
            level,
            order,
            tags,
            sources,
            updated,
            body);
    }

    // « Titre | https://… » : une URL absolue en https, jamais un lien relatif ou en http.
    private static KnowledgeSource ParseSource(string fileName, string value)
    {
        var separator = value.LastIndexOf('|');
        if (separator <= 0)
        {
            throw Error(fileName, $"source « {value} » : format « Titre | https://… » attendu.");
        }

        var title = value[..separator].Trim();
        var url = value[(separator + 1)..].Trim();
        if (title.Length == 0
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw Error(fileName, $"source « {value} » : titre requis et URL absolue en https.");
        }

        return new KnowledgeSource(title, url);
    }

    private static string Required(Dictionary<string, string> fields, string fileName, string key) =>
        fields.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : throw Error(fileName, $"champ d'en-tête requis manquant : « {key} ».");

    private static InvalidOperationException Error(string fileName, string message) =>
        new($"Article de la base documentaire « {fileName} » : {message}");
}
