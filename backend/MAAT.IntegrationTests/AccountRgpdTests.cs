using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 6 : cas de test 18 à 20. La purge doit
// couvrir les neuf tables de la chaîne documentée (EmailVerificationToken incluse) et
// laisser les tables de référence (Question, Recommendation, SectorWeight) intactes.
[Collection(AuthApiCollection.Name)]
public class AccountRgpdTests(AuthApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static readonly Guid SeededQuestionId = Guid.Parse("00000000-0000-0000-0004-000000000001");
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

    private async Task<(Guid CompanyId, Guid UserId, string Email, string AccessToken)> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        return (companyId, userId, email, accessToken);
    }

    // Seede un jeu complet de données company-scopées (une ligne par table de la chaîne
    // de purge, sauf User/Company déjà créés par l'inscription) pour que le test vérifie
    // une vraie purge, pas l'absence de données qui n'auraient jamais existé.
    //
    // is_completed=true et completed_at renseigné sur la DiagnosticRecommendation : ce
    // sont des champs saisis par l'utilisateur (case cochée dans le tableau de bord),
    // pas dérivés — le cas 18 doit vérifier qu'ils survivent dans l'export, pas
    // seulement que la ligne existe.
    private async Task<(Guid DiagnosticId, Guid ResponseId, Guid ReportId, Guid RecommendationId, DateTimeOffset RecommendationCompletedAt)> SeedCompanyDataAsync(
        HttpClient client, string accessToken, Guid userId)
    {
        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", accessToken, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var diagnosticId = created.GetProperty("id").GetGuid();

        Guid responseId;
        Guid reportId;
        Guid recommendationId;
        var recommendationCompletedAt = DateTimeOffset.UtcNow;
        await using (var context = fixture.CreateDbContext())
        {
            var response = new Response(diagnosticId, SeededQuestionId, 3);
            var domainScore = new DomainScore(diagnosticId, RseDomain.Environmental, 60m, 0.3m);
            var report = new Report(diagnosticId, userId);
            var recommendation = new Recommendation(
                $"REC-{Guid.NewGuid():N}"[..20],
                RseDomain.Environmental,
                "Mettre en place un suivi des émissions.",
                impactPoints: 5m,
                effortLevel: EffortLevel.Medium,
                triggerQuestionCode: SeededQuestionCode,
                triggerMaxValue: 2);
            var diagnosticRecommendation = new DiagnosticRecommendation(diagnosticId, recommendation.Id, priorityRank: 1)
            {
                IsCompleted = true,
                CompletedAt = recommendationCompletedAt,
            };

            context.Responses.Add(response);
            context.DomainScores.Add(domainScore);
            context.Reports.Add(report);
            context.Recommendations.Add(recommendation);
            context.DiagnosticRecommendations.Add(diagnosticRecommendation);
            await context.SaveChangesAsync();

            responseId = response.Id;
            reportId = report.Id;
            recommendationId = recommendation.Id;
        }

        return (diagnosticId, responseId, reportId, recommendationId, recommendationCompletedAt);
    }

    [Fact]
    public async Task Cas18_Export_contient_l_integralite_des_donnees_du_compte()
    {
        var client = fixture.CreateClient();
        var (_, userId, email, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, responseId, reportId, recommendationId, recommendationCompletedAt) =
            await SeedCompanyDataAsync(client, accessToken, userId);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        Assert.False(body.GetProperty("user").TryGetProperty("passwordHash", out _));

        Assert.Equal("Entreprise Test", body.GetProperty("company").GetProperty("name").GetString());

        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("id").GetGuid() == diagnosticId);
        Assert.Contains(
            body.GetProperty("responses").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == responseId);
        Assert.Contains(
            body.GetProperty("domainScores").EnumerateArray(),
            ds => ds.GetProperty("diagnosticId").GetGuid() == diagnosticId);
        Assert.Contains(
            body.GetProperty("reports").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == reportId);

        // is_completed/completed_at sont saisis par l'utilisateur, pas dérivés : leur
        // absence de l'export le rendrait incomplet au regard des art. 15 et 20.
        var diagnosticRecommendation = Assert.Single(
            body.GetProperty("diagnosticRecommendations").EnumerateArray(),
            dr => dr.GetProperty("diagnosticId").GetGuid() == diagnosticId
                && dr.GetProperty("recommendationId").GetGuid() == recommendationId);
        Assert.True(diagnosticRecommendation.GetProperty("isCompleted").GetBoolean());

        // PostgreSQL timestamptz tronque à la microseconde (DateTimeOffset va jusqu'au
        // tick, 100 ns) : une égalité stricte échouerait sur le dernier chiffre après
        // l'aller-retour en base, sans rapport avec la valeur perdue en export.
        Assert.Equal(
            recommendationCompletedAt,
            diagnosticRecommendation.GetProperty("completedAt").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Export_avec_mot_de_passe_de_confirmation_errone_repond_401()
    {
        var client = fixture.CreateClient();
        var (_, _, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = "MotDePasseErrone2026!" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cas19_Suppression_de_compte_ne_laisse_aucune_ligne_residuelle_dans_les_neuf_tables()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, email, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, responseId, reportId, _, _) = await SeedCompanyDataAsync(client, accessToken, userId);

        // Capture des jetons émis pendant l'inscription/connexion, avant suppression.
        await using (var preCheckContext = fixture.CreateDbContext())
        {
            Assert.True(await preCheckContext.RefreshTokens.AnyAsync(rt => rt.UserId == userId));
            Assert.True(await preCheckContext.EmailVerificationTokens.AnyAsync(t => t.UserId == userId));
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();

        Assert.False(await context.Users.AnyAsync(u => u.Id == userId));
        Assert.False(await context.RefreshTokens.AnyAsync(rt => rt.UserId == userId));
        Assert.False(await context.EmailVerificationTokens.AnyAsync(t => t.UserId == userId));
        Assert.False(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
        Assert.False(await context.Responses.AnyAsync(r => r.Id == responseId));
        Assert.False(await context.DomainScores.AnyAsync(ds => ds.DiagnosticId == diagnosticId));
        Assert.False(await context.DiagnosticRecommendations.AnyAsync(dr => dr.DiagnosticId == diagnosticId));
        Assert.False(await context.Reports.AnyAsync(r => r.Id == reportId));

        _ = email;
    }

    [Fact]
    public async Task Suppression_avec_mot_de_passe_de_confirmation_errone_repond_401_et_ne_supprime_rien()
    {
        var client = fixture.CreateClient();
        var (_, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = "MotDePasseErrone2026!" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.True(await context.Users.AnyAsync(u => u.Id == userId));
    }

    [Fact]
    public async Task Cas20_Suppression_de_compte_laisse_les_tables_de_reference_intactes()
    {
        var client = fixture.CreateClient();
        var (_, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, _, recommendationId, _) = await SeedCompanyDataAsync(client, accessToken, userId);

        int questionCountBefore;
        await using (var beforeContext = fixture.CreateDbContext())
        {
            questionCountBefore = await beforeContext.Questions.CountAsync();
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();

        // Table de référence : la Recommendation elle-même survit, seule la ligne de
        // jointure DiagnosticRecommendation (déjà vérifiée absente au cas 19) disparaît.
        Assert.True(await context.Recommendations.AnyAsync(r => r.Id == recommendationId));
        Assert.Equal(questionCountBefore, await context.Questions.CountAsync());
        Assert.True(await context.SectorWeights.AnyAsync());
    }
}
