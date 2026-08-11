using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 10 et 11. TimeProvider substitué par une horloge figée
// (ReportDeterminismApiFixture) : ce sont les deux cas où l'égalité d'octets doit être
// réellement vérifiée plutôt qu'assertée, conformément à la section 3 — c'est le cas 10 qui
// justifie de ne rien stocker.
[Collection(ReportDeterminismApiCollection.Name)]
public class ReportDeterminismTests(ReportDeterminismApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static readonly IReadOnlyDictionary<string, int> StandardAnswers = new Dictionary<string, int>
    {
        ["ENV-01"] = 5,
        ["ENV-02"] = 2,
        ["ENV-03"] = 0,
        [ReportDeterminismApiFixture.SocialQuestionCode] = 4,
        [ReportDeterminismApiFixture.EthicsQuestionCode] = 2,
        [ReportDeterminismApiFixture.ProcurementQuestionCode] = 1,
        [ReportDeterminismApiFixture.GovernanceQuestionCode] = 0,
    };

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Déterminisme Test",
        sectorCode = ReportDeterminismApiFixture.SectorCode,
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

    private async Task<(Guid UserId, string AccessToken)> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);
        return (userId, accessToken);
    }

    private async Task VerifyEmailDirectlyAsync(Guid userId)
    {
        await using var context = fixture.CreateDbContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        user.EmailVerified = true;
        await context.SaveChangesAsync();
    }

    private async Task<Guid> CompleteDiagnosticAsync(HttpClient client, string token)
    {
        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var (code, value) in StandardAnswers)
        {
            var answer = await client.SendAsync(AuthorizedRequest(HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{code}", token, new { value }));
            Assert.True(answer.IsSuccessStatusCode, $"Échec de réponse à {code} : {answer.StatusCode}");
        }

        var completeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        return diagnosticId;
    }

    private async Task<byte[]> FetchReportAsync(HttpClient client, Guid diagnosticId, string token)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task Cas10_Deux_generations_successives_horloge_figee_produisent_des_octets_identiques()
    {
        var client = fixture.CreateClient();
        var (userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);

        var first = await FetchReportAsync(client, diagnosticId, token);

        // Écart réel supérieur à 1 s avant la seconde génération : le format de date PDF
        // (/CreationDate, "D:YYYYMMDDHHmmSS...") n'a qu'une précision à la seconde — sans cet
        // écart, deux appels réellement séparés dans le temps mais tombant dans la même
        // seconde d'horloge produiraient la même chaîne même si TimeProvider n'était pas
        // réellement figé côté générateur, et ce test passerait par coïncidence de vitesse
        // plutôt que parce que le déterminisme est garanti par construction.
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        var second = await FetchReportAsync(client, diagnosticId, token);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Cas11_Modification_de_SectorWeight_apres_completion_laisse_le_document_identique()
    {
        var client = fixture.CreateClient();
        var (userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);
        var diagnosticId = await CompleteDiagnosticAsync(client, token);

        var before = await FetchReportAsync(client, diagnosticId, token);

        // Redistribue les cinq poids du secteur "4941A" (somme toujours 1, contrainte
        // différée respectée) : le rapport doit rester basé sur DomainScore.sector_weight,
        // jamais sur une relecture de SectorWeight (rapport-pdf.md, section 3).
        await using (var context = fixture.CreateDbContext())
        {
            var weights = await context.SectorWeights.Where(sw => sw.SectorCode == ReportDeterminismApiFixture.SectorCode).ToListAsync();
            foreach (var weight in weights)
            {
                weight.Weight = weight.Domain switch
                {
                    RseDomain.Environmental => 0.10m,
                    RseDomain.Social => 0.20m,
                    RseDomain.Ethics => 0.15m,
                    RseDomain.Procurement => 0.15m,
                    RseDomain.Governance => 0.40m,
                    _ => throw new InvalidOperationException("Domaine inattendu."),
                };
            }

            await context.SaveChangesAsync();
        }

        var after = await FetchReportAsync(client, diagnosticId, token);

        Assert.Equal(before, after);
    }
}
