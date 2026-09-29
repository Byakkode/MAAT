using MAAT.Application.Interfaces;
using MAAT.Domain.Knowledge;

namespace MAAT.Infrastructure.Knowledge;

// docs/specs/documentation.md, section 2 : les articles vivent dans le dépôt
// (Knowledge/Articles/*.md), embarqués dans l'assembly comme les polices du rapport. Aucune
// base de données ni fichier à déployer : un article se relit dans la pull request qui
// l'ajoute, et sa moindre correction est tracée par git.
public sealed class EmbeddedKnowledgeBase : IKnowledgeBase
{
    private const string ResourceMarker = ".Knowledge.Articles.";

    public IReadOnlyList<KnowledgeArticle> Articles { get; }

    private EmbeddedKnowledgeBase(IReadOnlyList<KnowledgeArticle> articles) => Articles = articles;

    // Chargé une fois, au démarrage de l'API (Program.cs) : un article mal formé fait échouer
    // le démarrage avec le nom du fichier, plutôt qu'une page vide en production.
    public static EmbeddedKnowledgeBase Load()
    {
        var assembly = typeof(EmbeddedKnowledgeBase).Assembly;
        var articles = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(ResourceMarker, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Ressource embarquée illisible : {name}.");
                using var reader = new StreamReader(stream);
                var fileName = name[(name.IndexOf(ResourceMarker, StringComparison.Ordinal) + ResourceMarker.Length)..];
                return KnowledgeArticleParser.Parse(fileName, reader.ReadToEnd());
            })
            .ToList();

        var duplicate = articles.GroupBy(a => a.Slug).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Base documentaire : slug « {duplicate.Key} » en double.");
        }

        return new EmbeddedKnowledgeBase(articles);
    }
}
