using System.Globalization;
using System.Text;

namespace MAAT.Domain.Knowledge;

// docs/specs/documentation.md, section 3 : recherche dans la base documentaire. Pure et en
// mémoire — la base compte quelques dizaines d'articles, chargés une fois au démarrage : un
// moteur de recherche (PostgreSQL full-text, Elasticsearch) serait disproportionné et
// ajouterait de l'infrastructure au VPS pour un gain nul à cette taille.
//
// Règles, pour que la recherche se comporte comme le lecteur s'y attend :
// - accents et majuscules ignorés (« decarbonation » trouve « Décarbonation ») ;
// - un début de mot suffit (« décarbo »), pour chercher au fil de la frappe ;
// - singulier et pluriel équivalents (« déchet » trouve « déchets ») ;
// - tous les mots de la recherche doivent être trouvés dans l'article, mots vides exceptés ;
// - un mot trouvé dans le titre pèse plus que dans les mots-clés, puis le résumé, puis le corps.
public static class KnowledgeSearch
{
    private const int TitleWeight = 10;
    private const int TagWeight = 6;
    private const int SummaryWeight = 4;
    private const int BodyWeight = 1;
    // Un mot répété cinquante fois dans un long article ne doit pas écraser un titre exact.
    private const int MaxBodyHits = 5;

    // Mots trop courants pour distinguer un article d'un autre.
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "au", "aux", "avec", "ce", "ces", "comment", "d", "dans", "de", "des", "du", "en", "est", "et",
        "l", "la", "le", "les", "leur", "ma", "mon", "ne", "par", "pas", "pour", "qu", "que", "quel", "quelle",
        "qui", "sa", "se", "ses", "son", "sur", "un", "une", "ou",
    };

    public static IReadOnlyList<KnowledgeArticle> Search(
        IReadOnlyList<KnowledgeArticle> articles,
        string? query,
        KnowledgeCategory? category = null,
        KnowledgeLevel? level = null)
    {
        var candidates = articles
            .Where(a => category is null || a.Category == category)
            .Where(a => level is null || a.Level == level);

        var terms = Tokenize(query ?? string.Empty).Where(t => !StopWords.Contains(t)).Distinct().ToList();

        // Sans mot significatif : tout, dans l'ordre du sommaire — rubrique, puis rang de lecture
        // dans la rubrique (titre en dernier recours, pour un ordre toujours déterminé).
        if (terms.Count == 0)
        {
            return [.. candidates
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Order)
                .ThenBy(a => a.Title, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase))];
        }

        return [.. candidates
            .Select(article => (Article: article, Score: Score(article, terms)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Article.Title, StringComparer.Ordinal)
            .Select(result => result.Article)];
    }

    // 0 dès qu'un mot de la recherche n'est trouvé nulle part : tous les mots sont exigés.
    private static int Score(KnowledgeArticle article, IReadOnlyList<string> terms)
    {
        var title = Tokenize(article.Title);
        var tags = article.Tags.SelectMany(Tokenize).ToList();
        var summary = Tokenize(article.Summary);
        var body = Tokenize(article.Body);

        var total = 0;
        foreach (var term in terms)
        {
            var score =
                (Contains(title, term) ? TitleWeight : 0)
                + (Contains(tags, term) ? TagWeight : 0)
                + (Contains(summary, term) ? SummaryWeight : 0)
                + Math.Min(MaxBodyHits, body.Count(word => Matches(word, term))) * BodyWeight;

            if (score == 0)
            {
                return 0;
            }

            total += score;
        }

        return total;
    }

    private static bool Contains(IEnumerable<string> words, string term) => words.Any(word => Matches(word, term));

    // Début de mot, pluriel en « s » ou « x » neutralisé de part et d'autre.
    private static bool Matches(string word, string term) =>
        word.StartsWith(term, StringComparison.Ordinal) || Singular(word) == Singular(term);

    private static string Singular(string word) =>
        word.Length > 3 && (word.EndsWith('s') || word.EndsWith('x')) ? word[..^1] : word;

    // Minuscules sans accents, découpé sur tout ce qui n'est ni lettre ni chiffre (l'apostrophe
    // sépare « l'énergie » en « l » et « energie »).
    internal static List<string> Tokenize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return [.. builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    }
}
