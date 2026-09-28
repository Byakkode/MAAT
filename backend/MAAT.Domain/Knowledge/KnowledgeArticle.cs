namespace MAAT.Domain.Knowledge;

// docs/specs/documentation.md : rubriques de la base documentaire, dans leur ordre
// d'affichage — du plus accessible (Premiers pas) au plus spécialisé.
public enum KnowledgeCategory
{
    GettingStarted,
    Regulation,
    Environment,
    Social,
    BusinessEthics,
    Procurement,
    Governance,
    Standards,
    Funding,
}

// Deux publics : le dirigeant de PME qui découvre la RSE (Essentials), le professionnel RSE
// qui cherche la précision réglementaire ou méthodologique (Expert).
public enum KnowledgeLevel
{
    Essentials,
    Expert,
}

// Toute affirmation d'un article s'appuie sur ses sources, affichées sous l'article : un
// produit RSE qui énonce une obligation légale doit pouvoir dire d'où il la tient.
public sealed record KnowledgeSource(string Title, string Url);

// Body : le contenu en Markdown, rendu par l'écran. UpdatedOn : date de la dernière
// vérification des sources — la réglementation RSE change vite, le lecteur doit savoir de
// quand date ce qu'il lit. Order : rang de lecture dans la rubrique (1 = à lire en premier),
// pour qu'une rubrique se parcoure comme un chemin plutôt que dans l'ordre alphabétique.
public sealed record KnowledgeArticle(
    string Slug,
    string Title,
    string Summary,
    KnowledgeCategory Category,
    KnowledgeLevel Level,
    int Order,
    IReadOnlyList<string> Tags,
    IReadOnlyList<KnowledgeSource> Sources,
    DateOnly UpdatedOn,
    string Body)
{
    // Vitesse de lecture d'un texte informatif en français : environ 200 mots par minute.
    private const int WordsPerMinute = 200;

    public int ReadingMinutes
    {
        get
        {
            var words = Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(1, (words + WordsPerMinute - 1) / WordsPerMinute);
        }
    }
}
