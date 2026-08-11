using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Pdf;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 1 à 7, 9, 19 et 20. Générateur réel (QuestPdfReportGenerator)
// et horloge réelle : ces cas portent sur le mécanisme HTTP de l'endpoint (préconditions,
// journalisation, rôles, quota) et sur le rendu réellement produit (polices, résolution
// d'image) — voir ReportContentApiFixture pour les cas de contenu (12 à 18) et
// ReportDeterminismApiFixture pour le déterminisme (10, 11).
[Collection(ReportApiCollection.Name)]
public class ReportTests(ReportApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static readonly IReadOnlyDictionary<string, int> StandardAnswers = new Dictionary<string, int>
    {
        ["ENV-01"] = 5,
        ["ENV-02"] = 2,
        ["ENV-03"] = 0,
        [ReportApiFixture.SocialQuestionCode] = 4,
        [ReportApiFixture.EthicsQuestionCode] = 2,
        [ReportApiFixture.ProcurementQuestionCode] = 1,
        [ReportApiFixture.GovernanceQuestionCode] = 0,
    };

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Rapport Test",
        sectorCode = ReportApiFixture.SectorCode,
        sizeRange = "Small",
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

    private async Task<string> AddUserWithRoleAndLoginAsync(Guid companyId, UserRole role, HttpClient client, bool emailVerified)
    {
        var email = UniqueEmail();
        var hasher = new BcryptPasswordHasher();

        await using (var context = fixture.CreateDbContext())
        {
            var user = new User(email, hasher.Hash(ValidPassword), companyId, role) { EmailVerified = emailVerified };
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task VerifyEmailDirectlyAsync(Guid userId)
    {
        await using var context = fixture.CreateDbContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        user.EmailVerified = true;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task<Guid> CompleteDiagnosticAsync(HttpClient client, string token, IReadOnlyDictionary<string, int> answers)
    {
        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var (code, value) in answers)
        {
            var answer = await client.SendAsync(AuthorizedRequest(HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{code}", token, new { value }));
            Assert.True(answer.IsSuccessStatusCode, $"Échec de réponse à {code} : {answer.StatusCode}");
        }

        var completeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        return diagnosticId;
    }

    // Inscription + connexion + diagnostic complété + adresse vérifiée : le scénario de base
    // réutilisé par la plupart des cas ci-dessous.
    private async Task<(Guid DiagnosticId, Guid UserId, string AccessToken)> CompleteVerifiedDiagnosticAsync(HttpClient client)
    {
        var (_, userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);
        var diagnosticId = await CompleteDiagnosticAsync(client, token, StandardAnswers);
        return (diagnosticId, userId, token);
    }

    [Fact]
    public async Task Cas1_Diagnostic_Completed_compte_verifie_retourne_200_application_pdf()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, _, token) = await CompleteVerifiedDiagnosticAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(fileName);
        Assert.Matches(@"^""?maat-diagnostic-entreprise-rapport-test-\d{4}-\d{2}-\d{2}\.pdf""?$", fileName!);
    }

    [Fact]
    public async Task Cas2_Diagnostic_InProgress_repond_409_sans_creer_de_Report()
    {
        var client = fixture.CreateClient();
        var (_, userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);

        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.Equal(0, await context.Reports.CountAsync(r => r.DiagnosticId == diagnosticId));
    }

    [Fact]
    public async Task Cas3_Diagnostic_Archived_repond_409()
    {
        var client = fixture.CreateClient();
        var (_, userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);

        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var abandonResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/abandon", token));
        Assert.Equal(HttpStatusCode.OK, abandonResponse.StatusCode);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cas4_Diagnostic_d_une_autre_entreprise_repond_404()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, _, _) = await CompleteVerifiedDiagnosticAsync(client);
        var (_, otherUserId, otherToken) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(otherUserId);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cas5_Compte_non_verifie_repond_403_sans_creer_de_Report()
    {
        var client = fixture.CreateClient();
        var (_, _, token) = await RegisterCompanyAndLoginAdminAsync(client);
        // Adresse jamais vérifiée, volontairement : la complétion du diagnostic reste
        // autorisée (auth-securite-rgpd.md, section 1 — seule la génération est bloquée).
        var diagnosticId = await CompleteDiagnosticAsync(client, token, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));

        await using var context = fixture.CreateDbContext();
        Assert.Equal(0, await context.Reports.CountAsync(r => r.DiagnosticId == diagnosticId));
    }

    [Fact]
    public async Task Cas6_Viewer_retourne_200()
    {
        var client = fixture.CreateClient();
        var (companyId, _, adminToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CompleteDiagnosticAsync(client, adminToken, StandardAnswers);
        // L'e-mail vérifié doit être celui du principal appelant (le Viewer), pas celui de
        // l'Admin qui a créé le diagnostic : la précondition porte sur currentUser.
        var viewerToken = await AddUserWithRoleAndLoginAsync(companyId, UserRole.Viewer, client, emailVerified: true);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", viewerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cas7_Generation_reussie_cree_une_ligne_Report_avec_generated_by_user_id_du_principal()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, userId, token) = await CompleteVerifiedDiagnosticAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        var report = await context.Reports.SingleAsync(r => r.DiagnosticId == diagnosticId);
        Assert.Equal(userId, report.GeneratedByUserId);
        Assert.Equal(ReportFormat.Pdf, report.Format);
    }

    [Fact]
    public async Task Cas9_Onzieme_generation_en_moins_de_15_minutes_repond_429()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, _, token) = await CompleteVerifiedDiagnosticAsync(client);

        for (var i = 0; i < 10; i++)
        {
            var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var eleventhResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventhResponse.StatusCode);
    }

    [Fact]
    public async Task Cas19_Le_PNG_du_radar_est_embarque_a_la_resolution_de_rendu()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, _, token) = await CompleteVerifiedDiagnosticAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        // UseOriginalImage (QuestPdfReportGenerator) embarque les pixels sans rééchantillonnage :
        // les dictionnaires d'image du PDF (non compressés, contrairement aux flux de contenu)
        // portent donc /Width et /Height à la résolution de rendu exacte.
        var text = Encoding.Latin1.GetString(bytes);
        var widthMatch = Regex.Match(text, @"/Width\s+(\d+)");
        var heightMatch = Regex.Match(text, @"/Height\s+(\d+)");

        Assert.True(widthMatch.Success, "Aucune ressource /Width trouvée dans le PDF — image non embarquée ?");
        Assert.True(heightMatch.Success, "Aucune ressource /Height trouvée dans le PDF — image non embarquée ?");
        Assert.Equal(RadarChartRenderer.RenderedSizePx, int.Parse(widthMatch.Groups[1].Value));
        Assert.Equal(RadarChartRenderer.RenderedSizePx, int.Parse(heightMatch.Groups[1].Value));
    }

    [Fact]
    public async Task Cas20_Poppins_et_Inter_sont_effectivement_embarquees()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, _, token) = await CompleteVerifiedDiagnosticAsync(client);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        // Les dictionnaires de police (/BaseFont) vivent en dehors des flux de contenu
        // compressés : une recherche de sous-chaîne y est fiable, contrairement au texte
        // affiché lui-même (voir CapturingReportGenerator pour la raison).
        var text = Encoding.Latin1.GetString(bytes);

        // Une recherche de sous-chaîne globale "Poppins"/"Inter" ne suffit pas : elle reste
        // vraie tant qu'UNE SEULE graisse de chaque famille est embarquée, donc ne détecte
        // jamais l'absence spécifique d'une des cinq polices réellement utilisées par
        // QuestPdfReportGenerator (QuestPdfBootstrapper en enregistre six, mais Poppins
        // Regular ne sert nulle part dans le document — voir son commentaire). Chaque
        // /BaseFont est vérifié individuellement par son nom PostScript (le préfixe de subset
        // à six lettres, ex. "AAAAAA+", varie à chaque génération et n'est donc pas vérifié).
        var baseFonts = Regex.Matches(text, @"/BaseFont\s*/([^ /\r\n>]+)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] expectedPostScriptNames =
        [
            "Poppins-Bold", "Poppins-SemiBold", "Poppins-Medium", "Inter-Regular", "Inter-Light",
        ];

        foreach (var expected in expectedPostScriptNames)
        {
            Assert.True(
                baseFonts.Any(name => name.EndsWith(expected, StringComparison.Ordinal)),
                $"Police '{expected}' absente des ressources /BaseFont du PDF : {string.Join(", ", baseFonts)}");
        }
    }
}
