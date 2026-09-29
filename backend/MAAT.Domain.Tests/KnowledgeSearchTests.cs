using MAAT.Domain.Knowledge;

namespace MAAT.Domain.Tests;

// docs/specs/documentation.md, section 3 : recherche dans la base documentaire. Pure, sans
// I/O : les articles lui sont passés en entrée.
public class KnowledgeSearchTests
{
    private static KnowledgeArticle Article(
        string slug,
        string title,
        string summary = "Résumé.",
        string body = "Contenu.",
        KnowledgeCategory category = KnowledgeCategory.GettingStarted,
        KnowledgeLevel level = KnowledgeLevel.Essentials,
        params string[] tags) =>
        new(slug, title, summary, category, level, Order: 1, tags, [new KnowledgeSource("Source", "https://example.org")], new DateOnly(2026, 9, 28), body);

    private static readonly KnowledgeArticle Carbon = Article(
        "bilan-carbone", "Réaliser son bilan carbone", "Mesurer ses émissions de gaz à effet de serre.",
        "Les scopes 1, 2 et 3 couvrent les émissions directes, l'énergie achetée et la chaîne de valeur.",
        KnowledgeCategory.Environment, KnowledgeLevel.Essentials, "décarbonation", "GES", "scope 3");

    private static readonly KnowledgeArticle Csrd = Article(
        "csrd", "CSRD et normes ESRS", "La directive européenne sur le reporting de durabilité.",
        "La CSRD impose un rapport de durabilité fondé sur la double matérialité. Les émissions de GES en font partie.",
        KnowledgeCategory.Regulation, KnowledgeLevel.Expert, "reporting", "ESRS");

    private static readonly KnowledgeArticle Waste = Article(
        "dechets", "Réduire et trier ses déchets", "Les obligations de tri et les filières REP.",
        "Le tri cinq flux concerne le papier, le métal, le plastique, le verre et le bois.",
        KnowledgeCategory.Environment, KnowledgeLevel.Essentials, "AGEC", "économie circulaire");

    private static readonly IReadOnlyList<KnowledgeArticle> All = [Waste, Csrd, Carbon];

    private static IEnumerable<string> Slugs(IEnumerable<KnowledgeArticle> results) => results.Select(a => a.Slug);

    [Fact]
    public void Sans_recherche_tous_les_articles_par_rubrique_puis_titre()
    {
        var results = KnowledgeSearch.Search(All, query: null);

        // Réglementation avant Environnement (ordre des rubriques), puis ordre alphabétique.
        Assert.Equal(["csrd", "bilan-carbone", "dechets"], Slugs(results));
    }

    // Une rubrique se lit comme un chemin : le rang de lecture l'emporte sur l'ordre alphabétique.
    [Fact]
    public void Sans_recherche_le_rang_de_lecture_prime_sur_le_titre()
    {
        var first = Carbon with { Slug = "z-a-lire-en-premier", Title = "Zéro déchet, par où commencer", Order = 1 };
        var second = Waste with { Order = 2 };

        Assert.Equal(["z-a-lire-en-premier", "dechets"], Slugs(KnowledgeSearch.Search([second, first], query: null)));
    }

    // Accents et majuscules ignorés : « decarbonation » trouve « décarbonation ».
    [Theory]
    [InlineData("decarbonation")]
    [InlineData("DÉCARBONATION")]
    [InlineData("Décarbonation")]
    public void Accents_et_majuscules_ignores(string query)
    {
        Assert.Equal(["bilan-carbone"], Slugs(KnowledgeSearch.Search(All, query)));
    }

    // Recherche au fil de la frappe : un début de mot suffit.
    [Fact]
    public void Un_debut_de_mot_suffit()
    {
        Assert.Equal(["bilan-carbone"], Slugs(KnowledgeSearch.Search(All, "décarbo")));
    }

    [Fact]
    public void Singulier_et_pluriel_equivalents()
    {
        Assert.Equal(["dechets"], Slugs(KnowledgeSearch.Search(All, "déchet")));
        Assert.Equal(["dechets"], Slugs(KnowledgeSearch.Search(All, "filière")));
    }

    // Plusieurs mots : un article doit tous les contenir, pas seulement l'un d'eux.
    [Fact]
    public void Tous_les_mots_doivent_etre_trouves()
    {
        Assert.Equal(["bilan-carbone"], Slugs(KnowledgeSearch.Search(All, "émissions scope")));
        Assert.Empty(KnowledgeSearch.Search(All, "émissions verre"));
    }

    // Un mot du titre pèse plus qu'un mot du corps : « GES » est un mot-clé du bilan carbone,
    // seulement cité dans le corps de l'article CSRD.
    [Fact]
    public void Titre_et_mots_cles_classes_avant_le_corps()
    {
        Assert.Equal(["bilan-carbone", "csrd"], Slugs(KnowledgeSearch.Search(All, "GES")));
    }

    // Les mots vides ne filtrent rien : « la », « de », « et » n'excluent aucun article.
    [Fact]
    public void Mots_vides_ignores()
    {
        Assert.Equal(["bilan-carbone"], Slugs(KnowledgeSearch.Search(All, "le bilan de la carbone")));
    }

    [Fact]
    public void Aucun_resultat()
    {
        Assert.Empty(KnowledgeSearch.Search(All, "blockchain"));
    }

    [Fact]
    public void Filtres_par_rubrique_et_par_niveau()
    {
        Assert.Equal(["bilan-carbone", "dechets"], Slugs(KnowledgeSearch.Search(All, null, KnowledgeCategory.Environment)));
        Assert.Equal(["csrd"], Slugs(KnowledgeSearch.Search(All, null, level: KnowledgeLevel.Expert)));
        Assert.Empty(KnowledgeSearch.Search(All, "déchets", KnowledgeCategory.Regulation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("la de et")]
    public void Recherche_vide_ou_seulement_de_mots_vides_renvoie_tout(string query)
    {
        Assert.Equal(3, KnowledgeSearch.Search(All, query).Count);
    }

    [Fact]
    public void Temps_de_lecture_arrondi_a_la_minute_superieure_et_au_moins_une_minute()
    {
        Assert.Equal(1, Article("court", "Court", body: "Trois mots seulement.").ReadingMinutes);
        Assert.Equal(2, Article("long", "Long", body: string.Join(' ', Enumerable.Repeat("mot", 201))).ReadingMinutes);
    }
}
