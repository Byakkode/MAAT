using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/dashboard.md, "Cas de test", cas 1 à 14 (endpoint, benchmark, plan d'actions —
// les cas 15 à 20 sont frontend, hors périmètre backend). Les cas 8 et 11 portent l'exigence
// RGPD d'anonymisation : vérifiés en scannant le corps brut de la réponse à la recherche
// d'identifiants, de noms ou de scores d'entreprises tierces, pas en asserte-t-on l'absence
// d'un champ précis.
[Collection(DashboardApiCollection.Name)]
public class DashboardTests(DashboardApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string DefaultSectorCode = "6201Z";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    // SectorCode est limité à 6 caractères (CompanyConfiguration) : "B" + 5 caractères
    // hexadécimal d'un Guid tient dans cette limite tout en restant unique par test — les cas
    // de benchmark (8 à 11) ne doivent jamais partager un secteur avec un autre test de cette
    // classe, sous peine de fausser l'effectif compté (même précaution que le cas 11 de
    // RecommendationsTests pour SectorWeight).
    private static string UniqueSectorCode() => "B" + Guid.NewGuid().ToString("N")[..5];

    private static object RegisterPayload(string email, string password, string sectorCode, string companyName) => new
    {
        email,
        password,
        companyName,
        sectorCode,
        sizeRange = "Micro",
        region = "Île-de-France",
    };

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<(Guid CompanyId, Guid UserId, string AccessToken)> RegisterCompanyAndLoginAdminAsync(
        HttpClient client, string sectorCode = DefaultSectorCode, string companyName = "Entreprise Test")
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword, sectorCode, companyName));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        return (companyId, userId, accessToken);
    }

    private async Task<string> AddViewerAndLoginAsync(HttpClient client, Guid companyId)
    {
        var email = UniqueEmail();
        var hasher = new BcryptPasswordHasher();

        await using (var context = fixture.CreateDbContext())
        {
            var user = new User(email, hasher.Hash(ValidPassword), companyId, UserRole.Viewer);
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<Guid> CreateDiagnosticAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", accessToken, new { }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, string accessToken, Guid diagnosticId, string questionCode, int value) =>
        client.SendAsync(AuthorizedRequest(
            HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{questionCode}", accessToken, new { value }));

    // Répond à toutes les questions actives (les 3 réelles + les 4 de ce fixture) avec la
    // même valeur : chaque domaine obtient alors le même score (value / 5 * 100), quelle que
    // soit sa pondération sectorielle — pratique pour piloter un score global précis dans les
    // tests de benchmark sans avoir à calculer une moyenne pondérée à la main.
    private async Task AnswerAllQuestionsUniformlyAsync(HttpClient client, string accessToken, Guid diagnosticId, int value)
    {
        foreach (var code in DashboardApiFixture.AllActiveQuestionCodes)
        {
            var response = await AnswerAsync(client, accessToken, diagnosticId, code, value);
            Assert.True(response.IsSuccessStatusCode, $"Échec de réponse à {code} : {response.StatusCode}");
        }
    }

    private static async Task CompleteAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AbandonAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/abandon", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Complète un diagnostic "au forfait" pour une entreprise dédiée au benchmark : seul le
    // score global compte, jamais son détail (voir cas 8 à 11), donc value / 5 * 100 suffit.
    private async Task<(Guid CompanyId, decimal GlobalScore)> RegisterAndCompleteWithUniformScoreAsync(
        HttpClient client, int value, string sectorCode, string companyName)
    {
        var (companyId, _, token) = await RegisterCompanyAndLoginAdminAsync(client, sectorCode, companyName);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, value);
        await CompleteAsync(client, token, diagnosticId);
        return (companyId, value * 20m);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body, string RawBody)> GetDashboardAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/dashboard", accessToken));
        var raw = await response.Content.ReadAsStringAsync();
        var body = raw.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(raw);
        return (response.StatusCode, body, raw);
    }

    // ---- Endpoint ----

    [Fact]
    public async Task Cas1_Aucun_diagnostic_repond_200_avec_hasCompletedDiagnostic_false()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        var (status, body, _) = await GetDashboardAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("hasCompletedDiagnostic").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("latestDiagnostic").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("inProgressDiagnostic").ValueKind);
        Assert.Empty(body.GetProperty("domainScores").EnumerateArray());
        Assert.Empty(body.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task Cas2_Diagnostic_InProgress_uniquement_repond_200_avec_avancement_sans_score()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, "ENV-01", 3);

        var (status, body, _) = await GetDashboardAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("hasCompletedDiagnostic").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("latestDiagnostic").ValueKind);

        var inProgress = body.GetProperty("inProgressDiagnostic");
        Assert.Equal(diagnosticId, inProgress.GetProperty("id").GetGuid());
        Assert.Equal(1, inProgress.GetProperty("answeredCount").GetInt32());
        Assert.Equal(7, inProgress.GetProperty("totalActiveQuestions").GetInt32());
    }

    [Fact]
    public async Task Cas3_Un_diagnostic_complete_donne_cinq_scores_de_domaine_et_un_historique_a_un_point()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 4);
        await CompleteAsync(client, token, diagnosticId);

        var (status, body, _) = await GetDashboardAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body.GetProperty("hasCompletedDiagnostic").GetBoolean());

        var latest = body.GetProperty("latestDiagnostic");
        Assert.Equal(diagnosticId, latest.GetProperty("id").GetGuid());
        Assert.Equal(80.00m, latest.GetProperty("globalScore").GetDecimal());
        Assert.Equal(DefaultSectorCode, latest.GetProperty("sectorCode").GetString());

        var domainScores = body.GetProperty("domainScores").EnumerateArray().ToList();
        Assert.Equal(5, domainScores.Count);
        var domains = domainScores.Select(d => d.GetProperty("domain").GetString()!).ToHashSet();
        Assert.Equal(Enum.GetNames<RseDomain>().ToHashSet(), domains);

        var history = body.GetProperty("history").EnumerateArray().ToList();
        var point = Assert.Single(history);
        Assert.Equal(80.00m, point.GetProperty("globalScore").GetDecimal());
        Assert.Equal(JsonValueKind.Null, point.GetProperty("deltaFromPrevious").ValueKind);
    }

    [Fact]
    public async Task Cas4_Plusieurs_diagnostics_donnent_un_historique_trie_du_plus_ancien_au_plus_recent()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        var firstId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, firstId, 1);
        await CompleteAsync(client, token, firstId);

        var secondId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, secondId, 4);
        await CompleteAsync(client, token, secondId);

        var (status, body, _) = await GetDashboardAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        var history = body.GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(2, history.Count);
        Assert.Equal(20.00m, history[0].GetProperty("globalScore").GetDecimal());
        Assert.Equal(80.00m, history[1].GetProperty("globalScore").GetDecimal());
        Assert.True(
            history[0].GetProperty("completedAt").GetDateTimeOffset() <= history[1].GetProperty("completedAt").GetDateTimeOffset());
        Assert.Equal(60.00m, history[1].GetProperty("deltaFromPrevious").GetDecimal());

        // Le dernier de l'historique sert de référence (docs/specs/dashboard.md, cas 4).
        var latest = body.GetProperty("latestDiagnostic");
        Assert.Equal(secondId, latest.GetProperty("id").GetGuid());
        Assert.Equal(80.00m, latest.GetProperty("globalScore").GetDecimal());
    }

    [Fact]
    public async Task Cas5_Diagnostic_Archived_est_absent_de_l_historique()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        var completedId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, completedId, 3);
        await CompleteAsync(client, token, completedId);

        var archivedId = await CreateDiagnosticAsync(client, token);
        await AbandonAsync(client, token, archivedId);

        var (status, body, _) = await GetDashboardAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        var history = body.GetProperty("history").EnumerateArray().ToList();
        var point = Assert.Single(history);
        Assert.Equal(60.00m, point.GetProperty("globalScore").GetDecimal());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("inProgressDiagnostic").ValueKind);
    }

    [Fact]
    public async Task Cas6_Le_tableau_de_bord_ne_montre_jamais_les_donnees_d_une_autre_entreprise()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticIdA = await CreateDiagnosticAsync(client, tokenA);
        await AnswerAllQuestionsUniformlyAsync(client, tokenA, diagnosticIdA, 1);
        await CompleteAsync(client, tokenA, diagnosticIdA);

        var (_, _, tokenB) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticIdB = await CreateDiagnosticAsync(client, tokenB);
        await AnswerAllQuestionsUniformlyAsync(client, tokenB, diagnosticIdB, 5);
        await CompleteAsync(client, tokenB, diagnosticIdB);

        var (_, bodyA, _) = await GetDashboardAsync(client, tokenA);
        var (_, bodyB, _) = await GetDashboardAsync(client, tokenB);

        Assert.Equal(20.00m, bodyA.GetProperty("latestDiagnostic").GetProperty("globalScore").GetDecimal());
        Assert.Equal(diagnosticIdA, bodyA.GetProperty("latestDiagnostic").GetProperty("id").GetGuid());
        Assert.Equal(100.00m, bodyB.GetProperty("latestDiagnostic").GetProperty("globalScore").GetDecimal());
        Assert.Equal(diagnosticIdB, bodyB.GetProperty("latestDiagnostic").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cas7_Viewer_repond_200()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        await AnswerAllQuestionsUniformlyAsync(client, adminToken, diagnosticId, 3);
        await CompleteAsync(client, adminToken, diagnosticId);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var (status, _, _) = await GetDashboardAsync(client, viewerToken);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    // ---- Benchmark ----

    [Fact]
    public async Task Cas8_Moins_de_5_entreprises_dans_le_secteur_laisse_le_benchmark_absent_avec_un_motif()
    {
        var client = fixture.CreateClient();
        var sector = UniqueSectorCode();

        string? selfToken = null;
        for (var i = 0; i < 4; i++)
        {
            var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, sector, $"Entreprise Benchmark8-{i}");
            var diagnosticId = await CreateDiagnosticAsync(client, token);
            await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 3);
            await CompleteAsync(client, token, diagnosticId);
            selfToken = token;
        }

        var (_, body, _) = await GetDashboardAsync(client, selfToken!);

        var benchmark = body.GetProperty("benchmark");
        Assert.False(benchmark.GetProperty("available").GetBoolean());
        Assert.Equal(4, benchmark.GetProperty("sampleSize").GetInt32());
        Assert.Equal(JsonValueKind.Null, benchmark.GetProperty("percentile").ValueKind);
        var reason = benchmark.GetProperty("reason").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public async Task Cas9_5_entreprises_ou_plus_donne_un_percentile_calcule()
    {
        var client = fixture.CreateClient();
        var sector = UniqueSectorCode();

        // Valeurs 1,2,4,5 -> scores 20,40,80,100 ; l'entreprise interrogée répond 3 -> 60.
        foreach (var value in new[] { 1, 2, 4, 5 })
        {
            await RegisterAndCompleteWithUniformScoreAsync(client, value, sector, $"Entreprise Benchmark9-{value}");
        }

        var (_, _, selfToken) = await RegisterCompanyAndLoginAdminAsync(client, sector, "Entreprise Benchmark9-self");
        var diagnosticId = await CreateDiagnosticAsync(client, selfToken);
        await AnswerAllQuestionsUniformlyAsync(client, selfToken, diagnosticId, 3);
        await CompleteAsync(client, selfToken, diagnosticId);

        var (_, body, _) = await GetDashboardAsync(client, selfToken);

        var benchmark = body.GetProperty("benchmark");
        Assert.True(benchmark.GetProperty("available").GetBoolean());
        Assert.Equal(5, benchmark.GetProperty("sampleSize").GetInt32());
        // 2 scores sous 60 (20, 40) sur 4 autres entreprises : round(100 * 2 / 4) = 50.
        Assert.Equal(50, benchmark.GetProperty("percentile").GetInt32());
        Assert.Equal(JsonValueKind.Null, benchmark.GetProperty("reason").ValueKind);
    }

    [Fact]
    public async Task Cas10_Une_entreprise_avec_plusieurs_diagnostics_completes_ne_compte_qu_une_fois()
    {
        var client = fixture.CreateClient();
        var sector = UniqueSectorCode();

        foreach (var value in new[] { 1, 2, 4 })
        {
            await RegisterAndCompleteWithUniformScoreAsync(client, value, sector, $"Entreprise Benchmark10-{value}");
        }

        // Cette entreprise complète un premier diagnostic à 100, puis un second à 20 : seul
        // le second (le plus récent) doit compter dans le benchmark (docs/specs/dashboard.md,
        // section 5 et cas 10).
        var (_, _, repeatToken) = await RegisterCompanyAndLoginAdminAsync(client, sector, "Entreprise Benchmark10-repeat");
        var repeatFirstId = await CreateDiagnosticAsync(client, repeatToken);
        await AnswerAllQuestionsUniformlyAsync(client, repeatToken, repeatFirstId, 5);
        await CompleteAsync(client, repeatToken, repeatFirstId);
        var repeatSecondId = await CreateDiagnosticAsync(client, repeatToken);
        await AnswerAllQuestionsUniformlyAsync(client, repeatToken, repeatSecondId, 1);
        await CompleteAsync(client, repeatToken, repeatSecondId);

        var (_, _, selfToken) = await RegisterCompanyAndLoginAdminAsync(client, sector, "Entreprise Benchmark10-self");
        var diagnosticId = await CreateDiagnosticAsync(client, selfToken);
        await AnswerAllQuestionsUniformlyAsync(client, selfToken, diagnosticId, 3);
        await CompleteAsync(client, selfToken, diagnosticId);

        var (_, body, _) = await GetDashboardAsync(client, selfToken);

        var benchmark = body.GetProperty("benchmark");
        // 5 entreprises au total (3 + la répétée + soi-même), jamais 6 : la répétée ne compte
        // qu'une fois malgré ses deux diagnostics complétés.
        Assert.Equal(5, benchmark.GetProperty("sampleSize").GetInt32());
        // Scores retenus des autres : 20, 40, 80, et 20 (le second diagnostic de la
        // répétée, pas son premier à 100) -> 3 sous 60 (20, 40, 20) sur 4 : round(100*3/4) = 75.
        Assert.Equal(75, benchmark.GetProperty("percentile").GetInt32());
    }

    [Fact]
    public async Task Cas11_Aucun_identifiant_nom_ni_score_individuel_d_une_autre_entreprise_ne_figure_dans_la_reponse()
    {
        var client = fixture.CreateClient();
        var sector = UniqueSectorCode();

        var others = new List<(Guid CompanyId, string Name, decimal Score)>();
        foreach (var value in new[] { 1, 2, 4, 5 })
        {
            var name = $"Entreprise Anonyme {value} {Guid.NewGuid():N}";
            var (companyId, score) = await RegisterAndCompleteWithUniformScoreAsync(client, value, sector, name);
            others.Add((companyId, name, score));
        }

        var (_, _, selfToken) = await RegisterCompanyAndLoginAdminAsync(client, sector, "Entreprise Anonyme Self");
        var diagnosticId = await CreateDiagnosticAsync(client, selfToken);
        // 3 -> 60 : sous le seuil de déclenchement (2) de toutes les recommandations du
        // fixture, donc plan d'actions vide — élimine tout bruit non pertinent à ce cas.
        await AnswerAllQuestionsUniformlyAsync(client, selfToken, diagnosticId, 3);
        await CompleteAsync(client, selfToken, diagnosticId);

        var (status, body, raw) = await GetDashboardAsync(client, selfToken);
        Assert.Equal(HttpStatusCode.OK, status);

        // Le benchmark doit malgré tout être exploitable (sinon ce cas ne prouve rien) :
        // sampleSize=5, percentile calculé sur les 4 autres, exactement comme le cas 9.
        var benchmark = body.GetProperty("benchmark");
        Assert.True(benchmark.GetProperty("available").GetBoolean());
        Assert.Equal(5, benchmark.GetProperty("sampleSize").GetInt32());

        foreach (var (companyId, name, score) in others)
        {
            Assert.DoesNotContain(companyId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(name, raw, StringComparison.Ordinal);
            // Format identique à celui attendu pour globalScore/history (voir cas 3 : "80.00"),
            // pour ne jamais rater une occurrence à cause d'un format différent.
            Assert.DoesNotContain(score.ToString("0.00"), raw, StringComparison.Ordinal);
        }
    }

    // ---- Plan d'actions ----

    [Fact]
    public async Task Cas12_Cinq_recommandations_au_maximum_triees_par_priority_rank()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        // Valeur 1 <= tous les seuils (2) : les 7 recommandations du fixture se déclenchent.
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 1);
        await CompleteAsync(client, token, diagnosticId);

        var (_, body, _) = await GetDashboardAsync(client, token);

        var items = body.GetProperty("actionPlan").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, items.Count);
        var ranks = items.Select(i => i.GetProperty("priorityRank").GetInt32()).ToList();
        Assert.Equal(ranks.OrderBy(r => r), ranks);
        Assert.Equal(ranks.Count, ranks.Distinct().Count());
    }

    [Fact]
    public async Task Cas13_Le_total_correspond_au_nombre_reel_de_recommandations_du_diagnostic()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 1);
        await CompleteAsync(client, token, diagnosticId);

        var (_, body, _) = await GetDashboardAsync(client, token);

        var actualCount = await CountDiagnosticRecommendationsAsync(diagnosticId);
        Assert.Equal(7, actualCount);
        Assert.Equal(actualCount, body.GetProperty("actionPlan").GetProperty("totalCount").GetInt32());
    }

    // Hors numérotation dashboard.md : ajouté avec triggeredRecommendationCount
    // (DomainScoreView), nécessaire au survol du radar (section 3, "le nombre de
    // recommandations qu'il [chaque domaine] a déclenchées") — sans ce champ, un domaine dont
    // les recommandations tombent hors des cinq premières de actionPlan.items ne pourrait pas
    // annoncer son propre total.
    [Fact]
    public async Task TriggeredRecommendationCount_porte_sur_le_plan_complet_pas_seulement_les_cinq_affichees()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        // Les 3 recommandations réelles (REC-ENV-01/02/03) sont toutes en Environmental ; les 4
        // du fixture en couvrent une chacune (Social, Ethics, Procurement, Governance).
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 1);
        await CompleteAsync(client, token, diagnosticId);

        var (_, body, _) = await GetDashboardAsync(client, token);

        var countByDomain = body.GetProperty("domainScores").EnumerateArray()
            .ToDictionary(d => d.GetProperty("domain").GetString()!, d => d.GetProperty("triggeredRecommendationCount").GetInt32());

        Assert.Equal(3, countByDomain["Environmental"]);
        Assert.Equal(1, countByDomain["Social"]);
        Assert.Equal(1, countByDomain["Ethics"]);
        Assert.Equal(1, countByDomain["Procurement"]);
        Assert.Equal(1, countByDomain["Governance"]);
        // Somme sur les cinq domaines = total du plan (cas 13), y compris les deux
        // recommandations Environmental hors des cinq premières affichées.
        Assert.Equal(7, countByDomain.Values.Sum());
    }

    [Fact]
    public async Task Cas14_Le_taux_d_avancement_reflete_les_actions_cochees()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, UniqueSectorCode());
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllQuestionsUniformlyAsync(client, token, diagnosticId, 1);
        await CompleteAsync(client, token, diagnosticId);

        var (_, bodyBefore, _) = await GetDashboardAsync(client, token);
        Assert.Equal(0, bodyBefore.GetProperty("actionPlan").GetProperty("completedCount").GetInt32());

        // REC-GOV-TEST a la priorité la plus basse (impact 2, le plus faible du fixture) :
        // presque certainement hors des cinq premières, ce qui prouve que le taux porte sur
        // l'ensemble du plan, pas seulement sur les éléments affichés.
        await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{DashboardApiFixture.RecGovernanceCode}", token,
            new { isCompleted = true }));
        await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/REC-ENV-01", token,
            new { isCompleted = true }));

        var (_, bodyAfter, _) = await GetDashboardAsync(client, token);

        Assert.Equal(2, bodyAfter.GetProperty("actionPlan").GetProperty("completedCount").GetInt32());
        Assert.Equal(7, bodyAfter.GetProperty("actionPlan").GetProperty("totalCount").GetInt32());
    }

    private async Task<int> CountDiagnosticRecommendationsAsync(Guid diagnosticId)
    {
        await using var context = fixture.CreateDbContext();
        return await context.DiagnosticRecommendations.CountAsync(dr => dr.DiagnosticId == diagnosticId);
    }
}
