using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/recommandations.md, cas 1 à 11 et 13 à 21. Le cas 12 (transaction) vit dans
// RecommendationSelectionFailureTests.cs — même raison que QuestionnaireScoringFailureTests
// pour le cas 15 de questionnaire.md : nécessite une substitution DI, incompatible avec un
// fixture partagé entre tous les tests de cette classe. Les cas 22 et 23 (seed) vivent dans
// RecommendationSeedTests.cs, contre la base réelle plutôt que ce fixture dédié.
[Collection(RecommendationsApiCollection.Name)]
public class RecommendationsTests(RecommendationsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password, string sectorCode) => new
    {
        email,
        password,
        companyName = "Entreprise Test",
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
        HttpClient client, string sectorCode = RecommendationsApiFixture.SectorCode)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword, sectorCode));
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

    // Répond à toutes les questions actives (docs/specs/referentiel.md : jamais une liste de
    // codes codée en dur) avec la valeur maximale (5) : aucun seuil de déclenchement du
    // fixture (tous ≤ 2) n'est atteint, donc rien ne se déclenche par défaut. Chaque test qui
    // a besoin d'un déclenchement force ensuite explicitement la question concernée à une
    // valeur plus basse.
    private async Task AnswerAllActiveQuestionsAsync(HttpClient client, string accessToken, Guid diagnosticId) =>
        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, accessToken, diagnosticId, defaultValue: 5);

    private static async Task<Guid> CompleteAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return diagnosticId;
    }

    private static async Task<List<JsonElement>> GetRecommendationsAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/recommendations", accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    private static List<string> CodesOf(List<JsonElement> recommendations) =>
        recommendations.Select(r => r.GetProperty("code").GetString()!).ToList();

    // ---- Déclenchement ----

    [Fact]
    public async Task Cas1_Reponse_strictement_inferieure_au_seuil_retient_la_recommandation()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);

        Assert.Contains(RecommendationsApiFixture.EnvLowCode, CodesOf(recommendations));
    }

    [Fact]
    public async Task Cas2_Reponse_egale_au_seuil_retient_la_recommandation()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 2);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);

        Assert.Contains(RecommendationsApiFixture.EnvLowCode, CodesOf(recommendations));
    }

    [Fact]
    public async Task Cas3_Reponse_superieure_au_seuil_ne_retient_pas_la_recommandation()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 3);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);

        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task Cas4_Recommandation_inactive_n_est_jamais_retenue()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        // Seuil de REC-INACTIVE = 5 : atteint par toute réponse, si elle était active.
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);

        Assert.DoesNotContain(RecommendationsApiFixture.InactiveCode, CodesOf(recommendations));
    }

    [Fact]
    public async Task Cas5_Diagnostic_sans_declenchement_donne_une_liste_vide_et_la_completion_reussit()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);

        var completeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);

        Assert.Empty(recommendations);
    }

    // ---- Priorisation ----

    [Fact]
    public async Task Cas6_Domaine_le_plus_lourd_passe_devant_a_impact_et_effort_egaux()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.SocialQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var codes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));

        Assert.True(
            codes.IndexOf(RecommendationsApiFixture.EnvLowCode) < codes.IndexOf(RecommendationsApiFixture.SocLowCode),
            $"Ordre obtenu : {string.Join(", ", codes)}");
    }

    [Fact]
    public async Task Cas7_Effort_Low_passe_devant_High_a_impact_et_domaine_egaux()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var codes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));

        Assert.True(
            codes.IndexOf(RecommendationsApiFixture.EnvLowCode) < codes.IndexOf(RecommendationsApiFixture.EnvHighCode),
            $"Ordre obtenu : {string.Join(", ", codes)}");
    }

    [Fact]
    public async Task Cas8_Priorites_egales_sont_departagees_par_code_croissant()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.GovernanceQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var codes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));

        Assert.Equal(new[] { RecommendationsApiFixture.TieACode, RecommendationsApiFixture.TieBCode }, codes);
    }

    [Fact]
    public async Task Cas9_Deux_executions_sur_les_memes_donnees_produisent_un_ordre_identique()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        async Task<List<string>> RunOnceAsync()
        {
            var diagnosticId = await CreateDiagnosticAsync(client, token);
            await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
            await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
            await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.SocialQuestionCode, 1);
            await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.GovernanceQuestionCode, 1);
            await CompleteAsync(client, token, diagnosticId);
            return CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));
        }

        var firstOrder = await RunOnceAsync();
        var secondOrder = await RunOnceAsync();

        Assert.Equal(firstOrder, secondOrder);
    }

    [Fact]
    public async Task Cas10_Priority_rank_commence_a_1_sans_trou_ni_doublon()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.SocialQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.GovernanceQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);
        var ranks = recommendations.Select(r => r.GetProperty("priorityRank").GetInt32()).ToList();

        Assert.Equal(ranks.Count, ranks.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, ranks.Count), ranks.OrderBy(r => r));
    }

    [Fact]
    public async Task Cas11_La_ponderation_utilisee_est_celle_persistee_pas_une_relecture_de_SectorWeight()
    {
        var client = fixture.CreateClient();
        // Secteur "6202A" plutôt que RecommendationsApiFixture.SectorCode : ce test mute les
        // poids sectoriels ci-dessous, une opération définitive dans ce fixture partagé par
        // toute la classe — l'isoler sur un secteur qu'aucun autre cas n'utilise évite de
        // fausser un test qui s'exécuterait après (même précaution que pour le cas 14).
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, sectorCode: "6202A");
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.SocialQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var beforeCodes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));
        Assert.Contains(RecommendationsApiFixture.EnvLowCode, beforeCodes);
        Assert.Contains(RecommendationsApiFixture.SocLowCode, beforeCodes);

        // Inverse délibérément les poids sectoriels du secteur après la complétion (en
        // préservant la somme à 1, exigée par une contrainte différée sur sector_weights) :
        // si l'ordre relisait SectorWeight, il s'inverserait lui aussi.
        await using (var context = fixture.CreateDbContext())
        {
            var environmental = await context.SectorWeights.SingleAsync(sw => sw.SectorCode == "6202A" && sw.Domain == RseDomain.Environmental);
            var social = await context.SectorWeights.SingleAsync(sw => sw.SectorCode == "6202A" && sw.Domain == RseDomain.Social);
            (environmental.Weight, social.Weight) = (social.Weight, environmental.Weight);
            await context.SaveChangesAsync();
        }

        var afterCodes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));

        Assert.Equal(beforeCodes, afterCodes);
    }

    // ---- Consultation ----

    [Fact]
    public async Task Cas13_Liste_triee_par_priority_rank()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.SocialQuestionCode, 1);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.GovernanceQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var recommendations = await GetRecommendationsAsync(client, token, diagnosticId);
        var ranks = recommendations.Select(r => r.GetProperty("priorityRank").GetInt32()).ToList();

        Assert.True(ranks.Count > 1);
        Assert.Equal(ranks.OrderBy(r => r), ranks);
    }

    [Fact]
    public async Task Cas14_Recommandation_desactivee_apres_completion_reste_presente_dans_la_liste()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);
        Assert.Contains(RecommendationsApiFixture.DeactivatableCode, CodesOf(await GetRecommendationsAsync(client, token, diagnosticId)));

        // Mutation directe et définitive : REC-DEACTIVATABLE n'est utilisée que par ce cas
        // (voir son commentaire dans RecommendationsApiFixture), la désactiver ici ne peut
        // donc pas fausser un autre test de cette classe qui s'exécuterait après.
        await using (var context = fixture.CreateDbContext())
        {
            var recommendation = await context.Recommendations.SingleAsync(r => r.Code == RecommendationsApiFixture.DeactivatableCode);
            recommendation.IsActive = false;
            await context.SaveChangesAsync();
        }

        var afterCodes = CodesOf(await GetRecommendationsAsync(client, token, diagnosticId));

        Assert.Contains(RecommendationsApiFixture.DeactivatableCode, afterCodes);
    }

    [Fact]
    public async Task Cas15_Consultation_d_un_diagnostic_d_une_autre_entreprise_repond_404()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, tokenB) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticIdB = await CreateDiagnosticAsync(client, tokenB);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticIdB}/recommendations", tokenA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cas16_Viewer_consulte_les_recommandations_repond_200()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        await AnswerAllActiveQuestionsAsync(client, adminToken, diagnosticId);
        await CompleteAsync(client, adminToken, diagnosticId);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/recommendations", viewerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Suivi ----

    [Fact]
    public async Task Cas17_Bascule_a_termine_renseigne_is_completed_et_completed_at()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var response = await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{RecommendationsApiFixture.EnvLowCode}", token, new { isCompleted = true }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task Cas18_Bascule_inverse_efface_is_completed_et_completed_at()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);
        var patchUrl = $"/api/diagnostics/{diagnosticId}/recommendations/{RecommendationsApiFixture.EnvLowCode}";
        await client.SendAsync(AuthorizedRequest(HttpMethod.Patch, patchUrl, token, new { isCompleted = true }));

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Patch, patchUrl, token, new { isCompleted = false }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task Cas19_Bascule_sur_un_diagnostic_Completed_est_autorisee()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var patchResponse = await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{RecommendationsApiFixture.EnvLowCode}", token, new { isCompleted = true }));
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}", token));
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cas20_Bascule_ne_modifie_jamais_le_global_score()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        await AnswerAsync(client, token, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, token, diagnosticId);

        var beforeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}", token));
        var scoreBefore = (await beforeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("globalScore").GetDecimal();

        var patchResponse = await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{RecommendationsApiFixture.EnvLowCode}", token, new { isCompleted = true }));
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var afterResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}", token));
        var scoreAfter = (await afterResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("globalScore").GetDecimal();

        Assert.Equal(scoreBefore, scoreAfter);
    }

    [Fact]
    public async Task Cas21_Viewer_tentant_une_bascule_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        await AnswerAllActiveQuestionsAsync(client, adminToken, diagnosticId);
        await AnswerAsync(client, adminToken, diagnosticId, RecommendationsApiFixture.EnvTestQuestionCode, 1);
        await CompleteAsync(client, adminToken, diagnosticId);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await client.SendAsync(AuthorizedRequest(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{RecommendationsApiFixture.EnvLowCode}", viewerToken, new { isCompleted = true }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
