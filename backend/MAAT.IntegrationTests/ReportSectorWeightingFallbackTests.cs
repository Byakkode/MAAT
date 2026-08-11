using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Pdf;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 23 : l'indicateur de pondération sectorielle de la page de
// garde a longtemps été dérivé de DomainScore.SectorWeight, ce qui se trompait précisément
// quand un seul domaine était actif — la renormalisation de scoring.md (cas 7) ramène alors le
// coefficient effectif à 1.00 que la pondération d'origine soit spécifique ou par défaut,
// rendant les deux cas indiscernables. Ce test reproduit exactement cette condition : conteneur
// dédié, seul le référentiel de base seedé (ENV-01/02/03, les trois dans le domaine
// Environnement — MAAT.Infrastructure/Seed/questions.csv), comme QuestionnaireScoringFailureTests
// et RecommendationSelectionFailureTests pour la même raison. IReportGenerator substitué par
// CapturingReportGenerator, comme ReportContentApiFixture.
public class ReportSectorWeightingFallbackTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string UncoveredSectorCode = "9999Z";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private readonly CapturingReportGenerator _reportGenerator = new();
    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                    ["Jwt:SigningKey"] = "sector-weighting-fallback-test-signing-key-32b",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReportGenerator>();
                services.AddSingleton<IReportGenerator>(_reportGenerator);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01/02/03 viennent de MAAT.Infrastructure/Seed/questions.csv, pas des migrations —
        // toutes les trois dans le domaine Environnement, donc un seul domaine actif ici.
        await new ReferenceDataSeeder(context).SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Secteur Non Couvert",
        sectorCode = UncoveredSectorCode,
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

    [Fact]
    public async Task Cas23_Secteur_non_couvert_avec_un_seul_domaine_actif_indique_la_ponderation_par_defaut()
    {
        var client = _factory.CreateClient();

        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("accessToken").GetString()!;
        var userId = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(token).Claims.Single(c => c.Type == "sub").Value);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            user.EmailVerified = true;
            await context.SaveChangesAsync();
        }

        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var code in new[] { "ENV-01", "ENV-02", "ENV-03" })
        {
            var answer = await client.SendAsync(
                AuthorizedRequest(HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{code}", token, new { value = 3 }));
            Assert.True(answer.IsSuccessStatusCode, $"Échec de réponse à {code} : {answer.StatusCode}");
        }

        var completeResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
            var diagnostic = await context.Diagnostics.SingleAsync(d => d.Id == diagnosticId);
            Assert.True(
                diagnostic.DefaultSectorWeightingApplied,
                "Le secteur n'est pas couvert par sector-weights.csv : le repli doit être persisté.");

            var domainScores = await context.DomainScores.Where(ds => ds.DiagnosticId == diagnosticId).ToListAsync();
            var singleDomainScore = Assert.Single(domainScores);
            Assert.Equal(RseDomain.Environmental, singleDomainScore.Domain);

            // La preuve du bug corrigé : la renormalisation ramène le coefficient effectif à
            // 1.00 ici, exactement comme elle l'aurait fait avec une pondération spécifique à
            // un seul domaine — SectorWeight ne permet donc pas de distinguer les deux cas.
            Assert.Equal(1.000m, singleDomainScore.SectorWeight);
        }

        var reportResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var data = _reportGenerator.LastData!;
        Assert.True(data.DefaultSectorWeightingApplied);

        var label = QuestPdfReportGenerator.ComputeSectorWeightingLabel(data.DefaultSectorWeightingApplied);
        Assert.Contains("par défaut", label, StringComparison.OrdinalIgnoreCase);
    }
}
