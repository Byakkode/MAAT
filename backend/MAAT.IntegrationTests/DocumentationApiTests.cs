using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Enums;

namespace MAAT.IntegrationTests;

// docs/specs/documentation.md, section 4 : la base documentaire à travers l'API, avec les
// articles réellement embarqués. Sommaire et recherche ouverts à toutes les offres, lecture
// d'un article à partir d'Essential.
[Collection(PlanLimitsApiCollection.Name)]
public class DocumentationApiTests(PlanLimitsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string KnownSlug = "quest-ce-que-la-rse";

    private async Task<string> RegisterAsync(HttpClient client, SubscriptionPlan plan)
    {
        var email = $"documentation-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = ValidPassword,
            companyName = "Entreprise Documentation",
            sectorCode = "6201Z",
            sizeRange = "Small",
            region = "Bretagne",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var companyId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "company_id").Value);

        await using var context = fixture.CreateDbContext();
        await TestSubscriptions.SetPlanAsync(context, companyId, plan);
        return token;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task Sans_authentification_401()
    {
        var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, "/api/documentation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, $"/api/documentation/{KnownSlug}", null)).StatusCode);
    }

    // Starter voit ce que la base contient : rubriques, titres, résumés, recherche.
    [Fact]
    public async Task Starter_sommaire_et_recherche_ouverts()
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, SubscriptionPlan.Starter);

        var response = await GetAsync(client, "/api/documentation", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var articles = body.GetProperty("articles").EnumerateArray().ToList();
        Assert.Contains(articles, a => a.GetProperty("slug").GetString() == KnownSlug);
        Assert.All(articles, a =>
        {
            Assert.False(a.TryGetProperty("body", out _), "Le sommaire ne transmet jamais le contenu d'un article.");
            Assert.True(a.GetProperty("readingMinutes").GetInt32() >= 1);
        });
        var gettingStarted = body.GetProperty("categories").EnumerateArray().Single(c => c.GetProperty("category").GetString() == "GettingStarted");
        Assert.True(gettingStarted.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task Recherche_insensible_aux_accents_et_filtre_par_rubrique()
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, SubscriptionPlan.Starter);

        var search = await (await GetAsync(client, "/api/documentation?q=responsabilite%20societale", token)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(search.GetProperty("articles").EnumerateArray(), a => a.GetProperty("slug").GetString() == KnownSlug);

        var filtered = await (await GetAsync(client, "/api/documentation?category=Regulation", token)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(filtered.GetProperty("articles").EnumerateArray(), a => Assert.Equal("Regulation", a.GetProperty("category").GetString()));

        var nothing = await (await GetAsync(client, "/api/documentation?q=zzzzzzzz", token)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(nothing.GetProperty("articles").EnumerateArray());
    }

    [Fact]
    public async Task Recherche_trop_longue_400()
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, SubscriptionPlan.Starter);

        var response = await GetAsync(client, $"/api/documentation?q={new string('a', 201)}", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Starter_lecture_d_un_article_refusee_avec_l_offre_requise()
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, SubscriptionPlan.Starter);

        var response = await GetAsync(client, $"/api/documentation/{KnownSlug}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal("Essential", body.GetProperty("requiredPlan").GetString());
    }

    [Fact]
    public async Task Essential_lit_l_article_avec_son_contenu_et_ses_sources()
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, SubscriptionPlan.Essential);

        var response = await GetAsync(client, $"/api/documentation/{KnownSlug}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var article = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Qu'est-ce que la RSE ?", article.GetProperty("title").GetString());
        Assert.Contains("ISO 26000", article.GetProperty("body").GetString());
        Assert.NotEmpty(article.GetProperty("sources").EnumerateArray());
        // Date seule (AAAA-MM-JJ), sans heure : la valeur change à chaque mise à jour de l'article.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", article.GetProperty("updatedOn").GetString());
    }

    // Une adresse erronée répond 404, jamais une invitation à changer d'offre.
    [Theory]
    [InlineData(SubscriptionPlan.Starter)]
    [InlineData(SubscriptionPlan.Professional)]
    public async Task Article_inconnu_404_quelle_que_soit_l_offre(SubscriptionPlan plan)
    {
        var client = fixture.CreateClient();
        var token = await RegisterAsync(client, plan);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, "/api/documentation/article-qui-n-existe-pas", token)).StatusCode);
    }
}
