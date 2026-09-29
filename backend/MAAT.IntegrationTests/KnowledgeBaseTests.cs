using System.Text.RegularExpressions;
using MAAT.Domain.Knowledge;
using MAAT.Infrastructure.Knowledge;

namespace MAAT.IntegrationTests;

// docs/specs/documentation.md, section 2 : les articles réellement embarqués, et le format
// qu'ils doivent respecter. Sans base de données. Ce sont ces tests qui garantissent, à
// chaque ajout d'article, qu'aucun ne disparaît en silence ou ne cite une source invalide.
public partial class KnowledgeBaseTests
{
    private static readonly IReadOnlyList<KnowledgeArticle> Articles = EmbeddedKnowledgeBase.Load().Articles;

    [GeneratedRegex(@"\]\(/documentation/([^)#\s]+)")]
    private static partial Regex InternalLink();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex AnyLink();

    private const string ValidHeader = """
        ---
        slug: exemple
        title: Titre
        summary: Résumé.
        category: GettingStarted
        level: Essentials
        order: 3
        tags: a, b
        updated: 2026-09-28
        source: Source officielle | https://example.org/page
        ---
        Contenu.
        """;

    [Fact]
    public void Les_articles_embarques_se_chargent_tous()
    {
        Assert.NotEmpty(Articles);
        Assert.Equal(Articles.Count, Articles.Select(a => a.Slug).Distinct().Count());
    }

    [Fact]
    public void La_rubrique_Premiers_pas_est_couverte_pour_chaque_niveau_de_lecture()
    {
        Assert.Contains(Articles, a => a.Category == KnowledgeCategory.GettingStarted && a.Level == KnowledgeLevel.Essentials);
    }

    [Fact]
    public void Chaque_article_a_au_moins_une_source_en_https_et_un_resume()
    {
        Assert.All(Articles, article =>
        {
            Assert.NotEmpty(article.Sources);
            Assert.All(article.Sources, s => Assert.StartsWith("https://", s.Url, StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(article.Summary));
            Assert.NotEmpty(article.Tags);
        });
    }

    // Règle éditoriale du site : aucun tiret cadratin dans le texte affiché (deux-points,
    // virgule ou parenthèses à la place).
    [Fact]
    public void Aucun_tiret_cadratin_dans_le_texte_affiche()
    {
        Assert.All(Articles, article =>
        {
            var displayed = string.Join('\n', [article.Title, article.Summary, .. article.Tags, .. article.Sources.Select(s => s.Title), article.Body]);
            Assert.False(displayed.Contains('—'), $"{article.Slug} contient un tiret cadratin (—).");
        });
    }

    // Deux articles au même rang rendraient l'ordre de lecture de la rubrique ambigu.
    [Fact]
    public void Les_rangs_de_lecture_sont_uniques_dans_chaque_rubrique()
    {
        Assert.All(Articles.GroupBy(a => a.Category), rubric =>
            Assert.Equal(rubric.Count(), rubric.Select(a => a.Order).Distinct().Count()));
    }

    // Un lien vers un autre article doit viser un article qui existe : un lien mort dans une
    // base documentaire se voit immédiatement.
    [Fact]
    public void Les_liens_internes_visent_des_articles_existants()
    {
        var slugs = Articles.Select(a => a.Slug).ToHashSet();

        Assert.All(Articles, article =>
        {
            foreach (Match link in InternalLink().Matches(article.Body))
            {
                Assert.True(slugs.Contains(link.Groups[1].Value), $"{article.Slug} : lien vers « {link.Groups[1].Value} », article inexistant.");
            }
        });
    }

    // Les liens vers des organismes extérieurs (guichets d'aide, sites publics) suivent la même
    // règle que les sources : https uniquement. Un lien est donc soit un autre article, soit
    // une adresse https, jamais http, mailto ou une adresse relative ambiguë.
    [Fact]
    public void Les_liens_du_corps_visent_un_article_ou_une_adresse_https()
    {
        Assert.All(Articles, article =>
        {
            foreach (Match link in AnyLink().Matches(article.Body))
            {
                var target = link.Groups[1].Value;
                Assert.True(
                    target.StartsWith("/documentation/", StringComparison.Ordinal) || target.StartsWith("https://", StringComparison.Ordinal),
                    $"{article.Slug} : lien « {target} », ni article ni adresse https.");
            }
        });
    }

    // Le titre de l'article est affiché par l'écran : un « # Titre » dans le corps le
    // doublerait, et casserait la hiérarchie des titres (un seul h1 par page).
    [Fact]
    public void Le_corps_ne_repete_pas_le_titre_de_niveau_1()
    {
        Assert.All(Articles, article =>
            Assert.DoesNotMatch(new Regex("^# ", RegexOptions.Multiline), article.Body));
    }

    [Fact]
    public void Un_en_tete_valide_est_lu_champ_par_champ()
    {
        var article = KnowledgeArticleParser.Parse("exemple.md", ValidHeader);

        Assert.Equal("exemple", article.Slug);
        Assert.Equal(KnowledgeCategory.GettingStarted, article.Category);
        Assert.Equal(KnowledgeLevel.Essentials, article.Level);
        Assert.Equal(3, article.Order);
        Assert.Equal(["a", "b"], article.Tags);
        Assert.Equal(new DateOnly(2026, 9, 28), article.UpdatedOn);
        Assert.Equal(new KnowledgeSource("Source officielle", "https://example.org/page"), Assert.Single(article.Sources));
        Assert.Equal("Contenu.", article.Body);
    }

    [Theory]
    [InlineData("source: Source officielle | https://example.org/page", "", "au moins une source")]
    [InlineData("https://example.org/page", "http://example.org/page", "https")]
    [InlineData("slug: exemple", "slug: autre-nom", "nom du fichier")]
    [InlineData("slug: exemple", "slug: Exemple_Majuscule", "slug invalide")]
    [InlineData("category: GettingStarted", "category: Inconnue", "rubrique inconnue")]
    [InlineData("level: Essentials", "level: Debutant", "niveau inconnu")]
    [InlineData("updated: 2026-09-28", "updated: 28/09/2026", "aaaa-mm-jj")]
    [InlineData("order: 3", "order: 0", "rang de lecture")]
    [InlineData("order: 3", "order: premier", "rang de lecture")]
    [InlineData("tags: a, b", "auteur: Moi", "champ d'en-tête inconnu")]
    [InlineData("title: Titre", "title:", "title")]
    [InlineData("Contenu.", "", "pas de contenu")]
    public void Un_en_tete_invalide_est_refuse_avec_le_nom_du_fichier(string replaced, string replacement, string expectedMessage)
    {
        var content = ValidHeader.Replace(replaced, replacement);

        var ex = Assert.Throws<InvalidOperationException>(() => KnowledgeArticleParser.Parse("exemple.md", content));

        Assert.Contains("exemple.md", ex.Message);
        Assert.Contains(expectedMessage, ex.Message);
    }
}
