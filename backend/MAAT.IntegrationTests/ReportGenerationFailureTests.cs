using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 8 : un échec de la génération ne doit laisser aucune ligne
// Report — IReportGenerator substitué par un faux qui lève, exactement comme
// RecommendationSelectionFailureTests substitue IRecommendationEngine et
// QuestionnaireScoringFailureTests substitue IScoringService (voir leur commentaire pour le
// même principe). Conteneur et jeton JWT dédiés, même raison que ces deux classes.
public class ReportGenerationFailureTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string SectorCode = "6201Z";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private readonly IReportGenerator _failingReportGenerator = Substitute.For<IReportGenerator>();
    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _failingReportGenerator
            .Generate(Arg.Any<ReportData>())
            .Throws(new InvalidOperationException("Échec simulé de la génération du rapport."));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                    ["Jwt:SigningKey"] = "report-generation-failure-test-signing-key-32b",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReportGenerator>();
                services.AddSingleton(_failingReportGenerator);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01/02/03 viennent de MAAT.Infrastructure/Seed/questions.csv, pas des migrations.
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
        companyName = "Entreprise Échec Rapport",
        sectorCode = SectorCode,
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
    public async Task Cas8_Echec_de_la_generation_ne_cree_aucune_ligne_Report()
    {
        var client = _factory.CreateClient();

        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("accessToken").GetString()!;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
            var user = await context.Users.SingleAsync(u => u.Email == email);
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

        var completeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var reportResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));

        Assert.False(
            reportResponse.IsSuccessStatusCode,
            "La génération aurait dû échouer : IReportGenerator substitué lève systématiquement.");

        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<MaatDbContext>();
        Assert.Equal(0, await assertContext.Reports.CountAsync(r => r.DiagnosticId == diagnosticId));
    }
}
