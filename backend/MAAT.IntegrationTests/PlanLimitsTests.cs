using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/abonnement.md, section 8, cas 19 à 31 : les droits de chaque offre, vérifiés à
// travers l'API. Les règles elles-mêmes sont testées ligne par ligne dans
// PlanEntitlementsTests (Domain) ; ici, on vérifie que chaque endpoint les applique.
[Collection(PlanLimitsApiCollection.Name)]
public class PlanLimitsTests(PlanLimitsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string SectorCode = "4941A";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    // plan null : aucune ligne Subscription (l'entreprise n'a encore rien choisi).
    private async Task<(Guid CompanyId, Guid UserId, string AccessToken)> RegisterAsync(
        HttpClient client, SubscriptionPlan? plan, ProviderSubscriptionState state = ProviderSubscriptionState.Active)
    {
        var email = UniqueEmail();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = ValidPassword,
            companyName = "Entreprise Test",
            sectorCode = SectorCode,
            sizeRange = "Micro",
            region = "Île-de-France",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        if (plan is { } chosen)
        {
            await SetPlanAsync(companyId, chosen, state);
        }

        return (companyId, userId, accessToken);
    }

    private async Task SetPlanAsync(Guid companyId, SubscriptionPlan plan, ProviderSubscriptionState state = ProviderSubscriptionState.Active)
    {
        await using var context = fixture.CreateDbContext();
        await TestSubscriptions.SetPlanAsync(context, companyId, plan, state);
    }

    private static async Task<HttpResponseMessage> CreateDiagnosticAsync(HttpClient client, string token) =>
        await client.SendAsync(Authorized(HttpMethod.Post, "/api/diagnostics", token, new { }));

    // Toutes les réponses à 0 : les 45 recommandations du référentiel réel se déclenchent
    // (trigger_max_value = 3), assez pour vérifier les limites de 3 et de 12.
    private static async Task<Guid> CompleteDiagnosticAsync(HttpClient client, string token)
    {
        var create = await CreateDiagnosticAsync(client, token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var diagnosticId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, defaultValue: 0);

        var complete = await client.SendAsync(Authorized(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return diagnosticId;
    }

    private static async Task AssertPlanRequiredAsync(HttpResponseMessage response, string requiredPlan)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal(requiredPlan, body.GetProperty("requiredPlan").GetString());
    }

    private static async Task<(List<JsonElement> Items, int TotalCount)> GetRecommendationsAsync(
        HttpClient client, string token, Guid diagnosticId)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/recommendations", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var total = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
        return (items, total);
    }

    private static async Task<JsonElement> GetDashboardAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, "/api/dashboard", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> CodeAtRankAsync(Guid diagnosticId, int rank)
    {
        await using var context = fixture.CreateDbContext();
        return await (
            from entry in context.DiagnosticRecommendations
            join recommendation in context.Recommendations on entry.RecommendationId equals recommendation.Id
            where entry.DiagnosticId == diagnosticId && entry.PriorityRank == rank
            select recommendation.Code).SingleAsync();
    }

    [Fact]
    public async Task Cas19_Starter_UnSeulDiagnosticComplete()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);
        await CompleteDiagnosticAsync(client, token);

        await AssertPlanRequiredAsync(await CreateDiagnosticAsync(client, token), "Essential");
    }

    [Fact]
    public async Task Cas19_Starter_DiagnosticAbandonneNeComptePas()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);

        var first = await CreateDiagnosticAsync(client, token);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var abandon = await client.SendAsync(Authorized(HttpMethod.Post, $"/api/diagnostics/{firstId}/abandon", token));
        Assert.Equal(HttpStatusCode.OK, abandon.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await CreateDiagnosticAsync(client, token)).StatusCode);
    }

    [Fact]
    public async Task Cas20_Essential_DiagnosticsIllimites()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        await CompleteDiagnosticAsync(client, token);

        Assert.Equal(HttpStatusCode.Created, (await CreateDiagnosticAsync(client, token)).StatusCode);
    }

    [Fact]
    public async Task Cas21_RecommandationsVisiblesSelonLOffre()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);

        var (starterItems, starterTotal) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal([1, 2, 3], starterItems.Select(r => r.GetProperty("priorityRank").GetInt32()));
        Assert.True(starterTotal > 12, $"Le référentiel devrait déclencher plus de 12 recommandations, pas {starterTotal}.");

        await SetPlanAsync(companyId, SubscriptionPlan.Essential);
        var (essentialItems, essentialTotal) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal(Enumerable.Range(1, 12), essentialItems.Select(r => r.GetProperty("priorityRank").GetInt32()));
        Assert.Equal(starterTotal, essentialTotal);

        var actionPlan = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/action-plan", token));
        Assert.Equal(12, (await actionPlan.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
        Assert.Equal(starterTotal.ToString(), actionPlan.Headers.GetValues("X-Total-Count").Single());

        var dashboard = await GetDashboardAsync(client, token);
        Assert.Equal(12, dashboard.GetProperty("actionPlan").GetProperty("totalCount").GetInt32());
        Assert.Equal(starterTotal, dashboard.GetProperty("actionPlan").GetProperty("triggeredCount").GetInt32());

        // Troisième palier : toutes les recommandations déclenchées.
        await SetPlanAsync(companyId, SubscriptionPlan.Professional);
        var (professionalItems, _) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal(Enumerable.Range(1, starterTotal), professionalItems.Select(r => r.GetProperty("priorityRank").GetInt32()));
        Assert.Equal(starterTotal, (await GetDashboardAsync(client, token)).GetProperty("actionPlan").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Cas22_Professional_EcritureSurUneRecommandationAuDelaDe12_Acceptee()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Professional);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var code = await CodeAtRankAsync(diagnosticId, 13);

        var response = await client.SendAsync(Authorized(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{code}", token, new { isCompleted = true }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cas22_EcritureHorsDesRecommandationsVisibles_404()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var hiddenCode = await CodeAtRankAsync(diagnosticId, 13);

        var response = await client.SendAsync(Authorized(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{hiddenCode}", token, new { isCompleted = true }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cas23_CocherUneAction_RefuseEnStarter_AccepteEnEssential()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var firstCode = await CodeAtRankAsync(diagnosticId, 1);
        var url = $"/api/diagnostics/{diagnosticId}/recommendations/{firstCode}";

        await AssertPlanRequiredAsync(
            await client.SendAsync(Authorized(HttpMethod.Patch, url, token, new { isCompleted = true })), "Essential");

        await SetPlanAsync(companyId, SubscriptionPlan.Essential);
        var accepted = await client.SendAsync(Authorized(HttpMethod.Patch, url, token, new { isCompleted = true }));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Cas24_PlanDActionsEnrichi_ReserveAProfessional_IndicateursOuvertsAEssential()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var firstCode = await CodeAtRankAsync(diagnosticId, 1);
        var actionPlanUrl = $"/api/diagnostics/{diagnosticId}/action-plan/{firstCode}";
        var progress = new { status = "InProgress", assignedTo = "Claire", dueDate = (DateTimeOffset?)null, notes = "à suivre" };
        var indicators = new { co2EmissionsTons = 12.5 };

        await AssertPlanRequiredAsync(
            await client.SendAsync(Authorized(HttpMethod.Patch, actionPlanUrl, token, progress)), "Professional");
        // norme-volontaire.md, cas 12 : la saisie des indicateurs est ouverte dès Essential.
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authorized(HttpMethod.Put, "/api/indicators/2025", token, indicators))).StatusCode);

        await SetPlanAsync(companyId, SubscriptionPlan.Professional);
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authorized(HttpMethod.Patch, actionPlanUrl, token, progress))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authorized(HttpMethod.Put, "/api/indicators/2025", token, indicators))).StatusCode);
    }

    [Fact]
    public async Task Cas25_Starter_ScoresParDomaineMasques()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);

        var dashboard = await GetDashboardAsync(client, token);
        Assert.Equal(0, dashboard.GetProperty("domainScores").GetArrayLength());
        Assert.Equal(JsonValueKind.Number, dashboard.GetProperty("latestDiagnostic").GetProperty("globalScore").ValueKind);

        await AssertPlanRequiredAsync(
            await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/domain-scores/Environmental", token)),
            "Essential");
    }

    [Theory]
    [InlineData(SubscriptionPlan.Starter, false)]
    [InlineData(SubscriptionPlan.Essential, false)]
    [InlineData(SubscriptionPlan.Professional, true)]
    public async Task Cas26_BenchmarkReserveAProfessional(SubscriptionPlan plan, bool expected)
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, plan);
        await CompleteDiagnosticAsync(client, token);

        var dashboard = await GetDashboardAsync(client, token);

        Assert.Equal(expected, dashboard.GetProperty("benchmark").ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Cas27_Starter_OuvrirUnTicketRefuse_LectureAutorisee()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);

        var create = new HttpRequestMessage(HttpMethod.Post, "/api/support/tickets")
        {
            Content = new MultipartFormDataContent
            {
                { new StringContent("bug"), "Type" },
                { new StringContent("Titre du ticket"), "Title" },
                { new StringContent("Description du problème rencontré."), "Description" },
            },
        };
        create.Headers.Add("Authorization", $"Bearer {token}");

        await AssertPlanRequiredAsync(await client.SendAsync(create), "Essential");
        Assert.Equal(HttpStatusCode.OK,
            (await client.SendAsync(Authorized(HttpMethod.Get, "/api/support/tickets", token))).StatusCode);
    }

    [Fact]
    public async Task Cas28_Impaye_GardeLesDroitsDeLOffre()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential, ProviderSubscriptionState.PastDue);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);

        var (items, _) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal(12, items.Count);
        Assert.Equal(HttpStatusCode.Created, (await CreateDiagnosticAsync(client, token)).StatusCode);
    }

    [Fact]
    public async Task Cas29_RetourAStarter_DonneesConserveesEtRetrouveesAuReabonnement()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterAsync(client, SubscriptionPlan.Professional);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var firstCode = await CodeAtRankAsync(diagnosticId, 1);
        var progress = new { status = "InProgress", assignedTo = "Claire", dueDate = (DateTimeOffset?)null, notes = (string?)null };
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(
            HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/action-plan/{firstCode}", token, progress))).StatusCode);

        await SetPlanAsync(companyId, SubscriptionPlan.Starter);

        var (starterItems, _) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal(3, starterItems.Count);
        Assert.Equal(0, (await GetDashboardAsync(client, token)).GetProperty("domainScores").GetArrayLength());
        await using (var context = fixture.CreateDbContext())
        {
            Assert.True(await context.ActionItemProgresses.AnyAsync(p => p.DiagnosticId == diagnosticId));
            Assert.Equal(5, await context.DomainScores.CountAsync(d => d.DiagnosticId == diagnosticId));
        }

        await SetPlanAsync(companyId, SubscriptionPlan.Professional);

        var (proItems, proTotal) = await GetRecommendationsAsync(client, token, diagnosticId);
        Assert.Equal(proTotal, proItems.Count);
        Assert.Equal(5, (await GetDashboardAsync(client, token)).GetProperty("domainScores").GetArrayLength());
    }

    [Fact]
    public async Task Cas30_AbonnementExposeOffreEffectiveEtDroits()
    {
        var client = fixture.CreateClient();
        var (companyId, _, token) = await RegisterAsync(client, plan: null);

        var noPlan = await GetSubscriptionAsync(client, token);
        Assert.Equal("Starter", noPlan.GetProperty("effectivePlan").GetString());
        Assert.True(noPlan.GetProperty("entitlements").GetProperty("canStartDiagnostic").GetBoolean());
        Assert.Equal(3, noPlan.GetProperty("entitlements").GetProperty("visibleRecommendations").GetInt32());

        await CompleteDiagnosticAsync(client, token);
        var afterDiagnostic = await GetSubscriptionAsync(client, token);
        Assert.False(afterDiagnostic.GetProperty("entitlements").GetProperty("canStartDiagnostic").GetBoolean());

        // Offre payante choisie mais pas encore payée : droits du Starter.
        await SetPlanAsync(companyId, SubscriptionPlan.Professional, ProviderSubscriptionState.Incomplete);
        var pending = await GetSubscriptionAsync(client, token);
        Assert.Equal("PendingPayment", pending.GetProperty("status").GetString());
        Assert.Equal("Starter", pending.GetProperty("effectivePlan").GetString());

        await SetPlanAsync(companyId, SubscriptionPlan.Professional);
        var professional = await GetSubscriptionAsync(client, token);
        Assert.Equal("Professional", professional.GetProperty("effectivePlan").GetString());
        var rights = professional.GetProperty("entitlements");
        Assert.True(rights.GetProperty("canStartDiagnostic").GetBoolean());
        Assert.True(rights.GetProperty("canEditActionPlan").GetBoolean());
        Assert.True(rights.GetProperty("canViewBenchmark").GetBoolean());
        Assert.True(rights.GetProperty("fullReport").GetBoolean());
    }

    private static async Task<JsonElement> GetSubscriptionAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, "/api/billing/subscription", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Cas31_RapportStarter_PageDeGardeReduite()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, token) = await RegisterAsync(client, SubscriptionPlan.Starter);
        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            user.EmailVerified = true;
            await context.SaveChangesAsync();
        }

        var diagnosticId = await CompleteDiagnosticAsync(client, token);
        var reportUrl = $"/api/diagnostics/{diagnosticId}/report";

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, reportUrl, token))).StatusCode);
        var starter = fixture.ReportGenerator.LastData!;
        Assert.False(starter.FullReport);
        Assert.Empty(starter.DomainScores);
        Assert.Empty(starter.Recommendations);
        Assert.Null(starter.Sustainability);
        Assert.Null(starter.PreviousDomainScores);

        await SetPlanAsync(companyId, SubscriptionPlan.Professional);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, reportUrl, token))).StatusCode);
        var full = fixture.ReportGenerator.LastData!;
        Assert.True(full.FullReport);
        Assert.Equal(5, full.DomainScores.Count);
        // Toutes visibles en Professional : le rapport retombe sur sa propre limite de vingt
        // lignes (rapport-pdf.md, section 4), avec le total déclenché.
        Assert.Equal(20, full.Recommendations.Count);
        Assert.True(full.TotalRecommendationCount > 20);
    }
}
