using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/questionnaire.md, sections 1, 2, 4, 5, 6 : cas de test 1 à 24. Le cas 15 est le
// plus important — c'est celui qui garantit qu'aucun diagnostic ne peut exister dans un
// état intermédiaire incohérent (échec du calcul de score → transaction annulée).
[Collection("QuestionnaireApi")]
public class QuestionnaireTests(QuestionnaireApiFixture fixture)
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
        HttpClient client, string sectorCode = "6201Z")
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

    private async Task<Guid> CreateDiagnosticAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", accessToken, new { }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    // Le flux d'inscription ne crée que des Admin (auth-securite-rgpd.md, section 1) : pour
    // tester un rôle Viewer, l'utilisateur est inséré directement en base dans l'entreprise
    // existante, puis authentifié via le flux normal — même principe que
    // TenantIsolationTests.AddUserWithRoleAndLoginAsync.
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

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, string accessToken, Guid diagnosticId, string questionCode, int value) =>
        client.SendAsync(AuthorizedRequest(
            HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{questionCode}", accessToken, new { value }));

    // Répond aux sept questions actives existantes (les trois seedées par référence + les
    // quatre de test de QuestionnaireApiFixture), une par domaine restant.
    private async Task AnswerAllActiveQuestionsAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        foreach (var code in new[]
        {
            "ENV-01", "ENV-02", "ENV-03",
            QuestionnaireApiFixture.SocialQuestionCode,
            QuestionnaireApiFixture.EthicsQuestionCode,
            QuestionnaireApiFixture.ProcurementQuestionCode,
            QuestionnaireApiFixture.GovernanceQuestionCode,
        })
        {
            var response = await AnswerAsync(client, accessToken, diagnosticId, code, 3);
            Assert.True(response.IsSuccessStatusCode, $"Échec de réponse à {code} : {response.StatusCode}");
        }
    }

    // ---- Cycle de vie ----

    [Fact]
    public async Task Cas1_Creation_sans_autre_diagnostic_en_cours_repond_201()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Cas2_Creation_alors_qu_un_diagnostic_est_deja_en_cours_repond_409_avec_son_identifiant()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var existingId = await CreateDiagnosticAsync(client, token);

        var second = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(existingId, body.GetProperty("existingDiagnosticId").GetGuid());
    }

    [Fact]
    public async Task Cas3_Abandon_fait_passer_le_statut_a_Archived_et_conserve_les_reponses()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 3);

        var abandon = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/abandon", token));
        Assert.Equal(HttpStatusCode.OK, abandon.StatusCode);

        var getResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}", token));
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Archived", body.GetProperty("status").GetString());

        await using var context = fixture.CreateDbContext();
        Assert.True(await context.Responses.AnyAsync(r => r.DiagnosticId == diagnosticId));
    }

    [Fact]
    public async Task Cas4_Modifier_une_reponse_sur_un_diagnostic_Completed_repond_409()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        var complete = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var response = await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Réponses ----

    [Fact]
    public async Task Cas5_Premiere_reponse_a_une_question_repond_201_avec_une_ligne_creee()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);

        var response = await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 3);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.Equal(1, await context.Responses.CountAsync(r => r.DiagnosticId == diagnosticId));
    }

    [Fact]
    public async Task Cas6_Seconde_reponse_a_la_meme_question_repond_200_et_ne_duplique_pas()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 3);

        var second = await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 5);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await using var context = fixture.CreateDbContext();
        var responses = await context.Responses.Where(r => r.DiagnosticId == diagnosticId).ToListAsync();
        var response = Assert.Single(responses);
        Assert.Equal(5, response.Value);
    }

    [Fact]
    public async Task Cas7_Valeur_hors_de_l_intervalle_0_5_repond_400()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);

        var response = await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 6);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cas8_Reponse_a_une_question_inactive_repond_400()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);

        var response = await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.InactiveQuestionCode, 3);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cas9_Reponse_sur_un_diagnostic_d_une_autre_entreprise_repond_404()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, tokenB) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticIdB = await CreateDiagnosticAsync(client, tokenB);

        var response = await AnswerAsync(client, tokenA, diagnosticIdB, QuestionnaireApiFixture.SocialQuestionCode, 3);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Complétion ----

    [Fact]
    public async Task Cas10_Completion_avec_des_reponses_manquantes_repond_400_avec_les_codes_manquants()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, "ENV-01", 4);
        await AnswerAsync(client, token, diagnosticId, "ENV-02", 3);
        await AnswerAsync(client, token, diagnosticId, "ENV-03", 2);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var missingCodes = body.GetProperty("missingQuestionCodes").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains(QuestionnaireApiFixture.SocialQuestionCode, missingCodes);
        Assert.Contains(QuestionnaireApiFixture.EthicsQuestionCode, missingCodes);
        Assert.Contains(QuestionnaireApiFixture.ProcurementQuestionCode, missingCodes);
        Assert.Contains(QuestionnaireApiFixture.GovernanceQuestionCode, missingCodes);
    }

    [Fact]
    public async Task Cas11_Completion_valide_ecrit_les_cinq_lignes_DomainScore_et_complete_le_diagnostic()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.Equal(5, await context.DomainScores.CountAsync(ds => ds.DiagnosticId == diagnosticId));

        var diagnostic = await context.Diagnostics.SingleAsync(d => d.Id == diagnosticId);
        Assert.Equal(DiagnosticStatus.Completed, diagnostic.Status);
        Assert.NotNull(diagnostic.GlobalScore);
        Assert.NotNull(diagnostic.CompletedAt);
    }

    [Fact]
    public async Task Cas12_Le_sector_weight_persiste_correspond_au_secteur_de_l_entreprise()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, sectorCode: "4941A");
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        var environmental = await context.DomainScores.SingleAsync(ds => ds.DiagnosticId == diagnosticId && ds.Domain == RseDomain.Environmental);
        Assert.Equal(0.400m, environmental.SectorWeight);
        var governance = await context.DomainScores.SingleAsync(ds => ds.DiagnosticId == diagnosticId && ds.Domain == RseDomain.Governance);
        Assert.Equal(0.100m, governance.SectorWeight);
    }

    [Fact]
    public async Task Cas13_Secteur_non_couvert_applique_et_persiste_la_ponderation_par_defaut()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client, sectorCode: "0000Z");
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        var domainScores = await context.DomainScores.Where(ds => ds.DiagnosticId == diagnosticId).ToListAsync();
        Assert.Equal(5, domainScores.Count);
        Assert.All(domainScores, ds => Assert.Equal(0.200m, ds.SectorWeight));
    }

    [Fact]
    public async Task Cas14_Second_appel_a_complete_repond_409_sans_ligne_supplementaire()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        var first = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.Equal(5, await context.DomainScores.CountAsync(ds => ds.DiagnosticId == diagnosticId));
    }

    // Cas 15 : voir QuestionnaireScoringFailureTests.cs. Il ne peut plus se construire par
    // simple insertion d'un secteur à pondération incomplète — une contrainte différée sur
    // sector_weights (migration AddSectorWeightCoverageConstraint) le rejette désormais en
    // base — et vit donc dans un fixture dédié qui substitue IScoringService.

    // ---- Reprise ----

    [Fact]
    public async Task Cas16_GetCurrent_avec_un_diagnostic_en_cours_le_retourne_avec_l_avancement()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, "ENV-01", 4);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/diagnostics/current", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(diagnosticId, body.GetProperty("id").GetGuid());
        Assert.Equal(1, body.GetProperty("answeredCount").GetInt32());
    }

    [Fact]
    public async Task Cas17_GetCurrent_sans_diagnostic_en_cours_repond_404()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/diagnostics/current", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Rôles ----

    [Fact]
    public async Task Cas18_Viewer_tentant_d_abandonner_un_diagnostic_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/abandon", viewerToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cas19_Viewer_tentant_d_enregistrer_une_reponse_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await AnswerAsync(client, viewerToken, diagnosticId, "ENV-01", 3);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cas20_Viewer_tentant_de_completer_un_diagnostic_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", viewerToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Questions ----

    [Fact]
    public async Task Cas21_GetQuestions_retourne_les_actives_triees_groupees_par_domaine_avec_reponse_existante()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAsync(client, token, diagnosticId, "ENV-01", 4);
        await AnswerAsync(client, token, diagnosticId, QuestionnaireApiFixture.SocialQuestionCode, 2);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/questions", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var questions = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        // La question inactive de la fixture ne doit jamais apparaître.
        Assert.DoesNotContain(questions, q => q.GetProperty("code").GetString() == QuestionnaireApiFixture.InactiveQuestionCode);

        // Ordre : groupé par domaine (ordre fixe de l'énumération RseDomain), trié par
        // display_order à l'intérieur d'un domaine.
        var codesInOrder = questions.Select(q => q.GetProperty("code").GetString()).ToList();
        Assert.Equal(
            new[]
            {
                "ENV-01", "ENV-02", "ENV-03",
                QuestionnaireApiFixture.SocialQuestionCode,
                QuestionnaireApiFixture.EthicsQuestionCode,
                QuestionnaireApiFixture.ProcurementQuestionCode,
                QuestionnaireApiFixture.GovernanceQuestionCode,
            },
            codesInOrder);

        // Réponse existante reflétée ; question non répondue porte une valeur nulle.
        var env01 = questions.Single(q => q.GetProperty("code").GetString() == "ENV-01");
        Assert.Equal(4, env01.GetProperty("value").GetInt32());
        var env02 = questions.Single(q => q.GetProperty("code").GetString() == "ENV-02");
        Assert.Equal(JsonValueKind.Null, env02.GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Cas22_GetQuestions_sur_un_diagnostic_d_une_autre_entreprise_repond_404()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, tokenB) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticIdB = await CreateDiagnosticAsync(client, tokenB);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticIdB}/questions", tokenA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cas23_GetQuestions_accessible_au_Viewer()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminToken);
        var viewerToken = await AddViewerAndLoginAsync(client, companyId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/questions", viewerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cas24_GetQuestions_fonctionne_en_lecture_seule_sur_un_diagnostic_Completed()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, token);
        await AnswerAllActiveQuestionsAsync(client, token, diagnosticId);
        var complete = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/questions", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var questions = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(7, questions.Count);
        Assert.All(questions, q => Assert.Equal(JsonValueKind.Number, q.GetProperty("value").ValueKind));
    }
}
