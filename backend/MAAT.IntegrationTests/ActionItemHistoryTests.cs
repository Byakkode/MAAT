using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/recommandations.md, section 4 bis : historique du suivi des actions, à travers
// l'API. Les règles elles-mêmes (champs modifiés, regroupement des notes, fenêtre de dix
// minutes) sont testées dans ActionItemHistoryTests (Domain).
[Collection(PlanLimitsApiCollection.Name)]
public class ActionItemHistoryTests(PlanLimitsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private sealed record Account(Guid CompanyId, Guid UserId, string Email, string Token);

    private async Task<Account> RegisterAsync(HttpClient client, SubscriptionPlan plan)
    {
        var email = $"historique-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = ValidPassword,
            companyName = "Entreprise Historique",
            sectorCode = "4941A",
            sizeRange = "Micro",
            region = "Île-de-France",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var token = await LoginAsync(client, email);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var account = new Account(
            Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value),
            Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value),
            email.ToLowerInvariant(),
            token);

        await SetPlanAsync(account.CompanyId, plan);
        return account;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private async Task SetPlanAsync(Guid companyId, SubscriptionPlan plan)
    {
        await using var context = fixture.CreateDbContext();
        await TestSubscriptions.SetPlanAsync(context, companyId, plan);
    }

    // Un second compte dans la même entreprise, avec le rôle voulu (même procédé
    // qu'AccountRgpdTests.AddUserToCompanyAsync).
    private async Task<Account> AddUserAsync(HttpClient client, Account owner, UserRole role)
    {
        var other = await RegisterAsync(client, SubscriptionPlan.Starter);
        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == other.UserId);
            user.CompanyId = owner.CompanyId;
            user.Role = role;
            await context.SaveChangesAsync();
            await context.Subscriptions.Where(s => s.CompanyId == other.CompanyId).ExecuteDeleteAsync();
            await context.Companies.Where(c => c.Id == other.CompanyId).ExecuteDeleteAsync();
        }

        return other with { CompanyId = owner.CompanyId, Token = await LoginAsync(client, other.Email) };
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    // Diagnostic complété avec des réponses à 0 : toutes les recommandations se déclenchent.
    private static async Task<(Guid DiagnosticId, string Code)> CompleteDiagnosticAsync(HttpClient client, string token)
    {
        var create = await client.SendAsync(Authorized(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var diagnosticId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, defaultValue: 0);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token))).StatusCode);

        var plan = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/action-plan", token));
        var code = (await plan.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("code").GetString()!;
        return (diagnosticId, code);
    }

    private static async Task PatchAsync(HttpClient client, string token, Guid diagnosticId, string code,
        string status = "Planned", string? assignedTo = null, string? dueDate = null, string? notes = null)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/action-plan/{code}", token,
            new { status, assignedTo, dueDate, notes }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<JsonElement>> GetHistoryAsync(HttpClient client, string token, Guid diagnosticId, string code)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/action-plan/{code}/history", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static string Describe(JsonElement line) =>
        $"{line.GetProperty("field").GetString()}:{Str(line, "oldValue")}->{Str(line, "newValue")}";

    private static string Str(JsonElement line, string name) =>
        line.GetProperty(name).ValueKind == JsonValueKind.Null ? "∅" : line.GetProperty(name).GetString()!;

    [Fact]
    public async Task Chaque_champ_modifie_est_trace_avec_son_auteur_du_plus_recent_au_plus_ancien()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);

        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress", assignedTo: "Claire Martin");
        await PatchAsync(client, owner.Token, diagnosticId, code, "Done", assignedTo: "Claire Martin", dueDate: "2026-11-15");

        var history = await GetHistoryAsync(client, owner.Token, diagnosticId, code);

        Assert.Equal(
            ["Status:InProgress->Done", "DueDate:∅->2026-11-15", "Status:Planned->InProgress", "AssignedTo:∅->Claire Martin"],
            history.Select(Describe));
        Assert.All(history, line => Assert.Equal(owner.Email, line.GetProperty("changedBy").GetString()));
    }

    [Fact]
    public async Task Une_modification_sans_changement_n_ajoute_rien()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);

        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress", assignedTo: "Claire");
        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress", assignedTo: "Claire");

        Assert.Equal(2, (await GetHistoryAsync(client, owner.Token, diagnosticId, code)).Count);
    }

    // Enregistrement automatique pendant la frappe : plusieurs enregistrements, une seule ligne,
    // et jamais le texte des notes, ni en base ni dans la réponse.
    [Fact]
    public async Task Notes_une_seule_ligne_pour_une_redaction_et_jamais_leur_texte()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);

        await PatchAsync(client, owner.Token, diagnosticId, code, notes: "Devis");
        await PatchAsync(client, owner.Token, diagnosticId, code, notes: "Devis demandé");
        await PatchAsync(client, owner.Token, diagnosticId, code, notes: "Devis demandé au fournisseur confidentiel");

        var history = await GetHistoryAsync(client, owner.Token, diagnosticId, code);
        Assert.Equal(["Notes:∅->∅"], history.Select(Describe));
        Assert.DoesNotContain("Devis", history[0].GetRawText());

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.ActionItemChanges.AnyAsync(c => (c.OldValue ?? "").Contains("Devis") || (c.NewValue ?? "").Contains("Devis")));
    }

    // Un changement de statut entre deux rédactions les sépare.
    [Fact]
    public async Task Notes_separees_par_un_autre_changement_donnent_deux_lignes()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);

        await PatchAsync(client, owner.Token, diagnosticId, code, notes: "a");
        await PatchAsync(client, owner.Token, diagnosticId, code, "Blocked", notes: "a");
        await PatchAsync(client, owner.Token, diagnosticId, code, "Blocked", notes: "a b");

        Assert.Equal(
            ["Notes:∅->∅", "Status:Planned->Blocked", "Notes:∅->∅"],
            (await GetHistoryAsync(client, owner.Token, diagnosticId, code)).Select(Describe));
    }

    [Theory]
    [InlineData(SubscriptionPlan.Starter)]
    [InlineData(SubscriptionPlan.Essential)]
    public async Task Lecture_reservee_a_Professional(SubscriptionPlan plan)
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);
        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress");

        await SetPlanAsync(owner.CompanyId, plan);
        var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/action-plan/{code}/history", owner.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal("Professional", body.GetProperty("requiredPlan").GetString());
    }

    [Fact]
    public async Task Viewer_lit_l_historique()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);
        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress");
        var viewer = await AddUserAsync(client, owner, UserRole.Viewer);

        Assert.Single(await GetHistoryAsync(client, viewer.Token, diagnosticId, code));
    }

    [Fact]
    public async Task Diagnostic_d_une_autre_entreprise_404()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);
        var stranger = await RegisterAsync(client, SubscriptionPlan.Professional);

        var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/action-plan/{code}/history", stranger.Token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Droit à l'effacement (auth-securite-rgpd.md, section 6) : le compte supprimé disparaît de
    // l'historique, ses lignes restent pour l'entreprise, sans auteur.
    [Fact]
    public async Task Compte_supprime_lignes_conservees_sans_auteur()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);
        var colleague = await AddUserAsync(client, owner, UserRole.User);
        await PatchAsync(client, colleague.Token, diagnosticId, code, "InProgress");

        var delete = Authorized(HttpMethod.Delete, "/api/me", colleague.Token, new { password = ValidPassword });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        var line = Assert.Single(await GetHistoryAsync(client, owner.Token, diagnosticId, code));
        Assert.Equal("Status:Planned->InProgress", Describe(line));
        Assert.Equal(JsonValueKind.Null, line.GetProperty("changedBy").ValueKind);
    }

    // L'historique part avec l'entreprise.
    [Fact]
    public async Task Suppression_de_l_entreprise_supprime_l_historique()
    {
        var client = fixture.CreateClient();
        var owner = await RegisterAsync(client, SubscriptionPlan.Professional);
        var (diagnosticId, code) = await CompleteDiagnosticAsync(client, owner.Token);
        await PatchAsync(client, owner.Token, diagnosticId, code, "InProgress");

        var delete = Authorized(HttpMethod.Delete, "/api/me", owner.Token, new { password = ValidPassword });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.ActionItemChanges.AnyAsync(c => c.DiagnosticId == diagnosticId));
    }
}
