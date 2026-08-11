using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 4 : cas de test 13 à 17. Ce sont, avec les
// cas 1 à 12 déjà couverts par AuthTests, les tests qui prouvent que le cloisonnement
// par entreprise est vérifié par le code et non promis par une phrase de rapport.
[Collection(TenantIsolationApiCollection.Name)]
public class TenantIsolationTests(TenantIsolationApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    // ENV-01 (voir MAAT.Infrastructure/Seed/questions.csv), réutilisée pour ne pas
    // dupliquer les données de référence du questionnaire dans ces tests. Id non figé
    // (ReferenceDataSeeder génère un Guid à l'insertion) : résolu à l'exécution.
    private const string SeededQuestionCode = "ENV-01";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Test",
        sectorCode = "6201Z",
        sizeRange = "Micro",
        region = "Île-de-France",
    };

    private static HttpRequestMessage AuthorizedJsonRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    // Inscription (crée toujours un Admin, cf. section 1) + connexion. Retourne
    // company_id et user_id extraits des claims du JWT, jamais d'une entrée client.
    private async Task<(Guid CompanyId, Guid UserId, string AccessToken)> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
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

    // Le flux d'inscription ne crée que des Admin (section 1) : pour tester les rôles
    // User/Viewer, l'utilisateur est inséré directement en base dans une entreprise
    // existante, puis authentifié via le flux normal.
    private async Task<string> AddUserWithRoleAndLoginAsync(Guid companyId, UserRole role, HttpClient client)
    {
        var email = UniqueEmail();
        var hasher = new BcryptPasswordHasher();

        await using (var context = fixture.CreateDbContext())
        {
            var user = new User(email, hasher.Hash(ValidPassword), companyId, role);
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Cas13_Diagnostic_d_une_autre_entreprise_repond_404()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, tokenB) = await RegisterCompanyAndLoginAdminAsync(client);

        var createResponse = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Post, "/api/diagnostics", tokenB, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var diagnosticId = created.GetProperty("id").GetGuid();

        var response = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}", tokenA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cas14_Response_DomainScore_et_Report_d_une_autre_entreprise_repondent_404()
    {
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, userBId, tokenB) = await RegisterCompanyAndLoginAdminAsync(client);

        var createResponse = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Post, "/api/diagnostics", tokenB, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var diagnosticId = created.GetProperty("id").GetGuid();

        Guid responseId;
        Guid reportId;
        await using (var context = fixture.CreateDbContext())
        {
            var seededQuestionId = await context.Questions.Where(q => q.Code == SeededQuestionCode).Select(q => q.Id).SingleAsync();
            var response = new Response(diagnosticId, seededQuestionId, 3);
            var domainScore = new DomainScore(diagnosticId, RseDomain.Environmental, 60m, 0.3m, 180m, 300m);
            var report = new Report(diagnosticId, userBId);

            context.Responses.Add(response);
            context.DomainScores.Add(domainScore);
            context.Reports.Add(report);
            await context.SaveChangesAsync();

            responseId = response.Id;
            reportId = report.Id;
        }

        var responseResult = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Get, $"/api/responses/{responseId}", tokenA));
        var domainScoreResult = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/domain-scores/Environmental", tokenA));
        var reportResult = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Get, $"/api/reports/{reportId}", tokenA));

        Assert.Equal(HttpStatusCode.NotFound, responseResult.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, domainScoreResult.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reportResult.StatusCode);
    }

    [Fact]
    public async Task Cas15_Company_id_falsifie_dans_le_corps_est_ignore()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        var otherCompanyId = Guid.NewGuid();

        var createResponse = await client.SendAsync(
            AuthorizedJsonRequest(HttpMethod.Post, "/api/diagnostics", token, new { companyId = otherCompanyId }));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(companyId, created.GetProperty("companyId").GetGuid());
    }

    [Fact]
    public async Task Cas16_Viewer_tentant_de_creer_un_diagnostic_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _) = await RegisterCompanyAndLoginAdminAsync(client);
        var viewerToken = await AddUserWithRoleAndLoginAsync(companyId, UserRole.Viewer, client);

        var response = await client.SendAsync(AuthorizedJsonRequest(HttpMethod.Post, "/api/diagnostics", viewerToken, new { }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cas17_User_tentant_d_inviter_un_utilisateur_repond_403()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _) = await RegisterCompanyAndLoginAdminAsync(client);
        var userToken = await AddUserWithRoleAndLoginAsync(companyId, UserRole.User, client);

        var response = await client.SendAsync(
            AuthorizedJsonRequest(HttpMethod.Post, "/api/users/invite", userToken, new { email = UniqueEmail(), role = "User" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
