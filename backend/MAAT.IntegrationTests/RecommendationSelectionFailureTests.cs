using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
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

// docs/specs/recommandations.md, cas 12 — le pendant, pour la génération des
// recommandations, du cas 15 de questionnaire.md : un échec de la sélection doit annuler
// toute la transaction de complétion, y compris les DomainScore déjà écrits. IRecommendationEngine
// est substitué par un faux qui lève, exactement comme QuestionnaireScoringFailureTests
// substitue IScoringService — même raison : isoler la garantie transactionnelle testée ici
// de ce qui peut, en pratique, faire échouer la sélection ou la priorisation elle-même.
//
// Conteneur et jeton JWT dédiés, comme QuestionnaireApiFixture et RecommendationsApiFixture.
// S'appuie sur les seules questions et recommandations de référence (QuestionConfiguration,
// RecommendationConfiguration) : pas besoin de reproduire la couverture des cinq domaines
// ici, IRecommendationEngine substitué lève quelle que soit son entrée.
public class RecommendationSelectionFailureTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private readonly IRecommendationEngine _failingRecommendationEngine = Substitute.For<IRecommendationEngine>();
    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _failingRecommendationEngine
            .SelectTriggered(Arg.Any<IReadOnlyList<Recommendation>>(), Arg.Any<IReadOnlyDictionary<string, int>>())
            .Throws(new MissingTriggerResponseException("ENV-01"));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                    ["Jwt:SigningKey"] = "recommendation-failure-test-signing-key-32-bytes",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRecommendationEngine>();
                services.AddSingleton(_failingRecommendationEngine);
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

    [Fact]
    public async Task Cas12_Echec_de_la_selection_annule_la_completion_integralement()
    {
        var client = _factory.CreateClient();

        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("accessToken").GetString()!;

        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // IRecommendationEngine substitué lève quel que soit son entrée : peu importe la
        // valeur répondue, mais toutes les questions actives doivent l'être pour que la
        // complétion dépasse le contrôle de complétude (docs/specs/referentiel.md : jamais
        // une liste de codes codée en dur).
        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, defaultValue: 3);

        var completeResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));

        Assert.False(
            completeResponse.IsSuccessStatusCode,
            "La complétion aurait dû échouer : IRecommendationEngine substitué lève systématiquement.");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();

        var diagnostic = await context.Diagnostics.SingleAsync(d => d.Id == diagnosticId);
        Assert.Equal(DiagnosticStatus.InProgress, diagnostic.Status);
        Assert.Null(diagnostic.GlobalScore);
        Assert.Null(diagnostic.CompletedAt);

        Assert.Equal(0, await context.DomainScores.CountAsync(ds => ds.DiagnosticId == diagnosticId));
        Assert.Equal(0, await context.DiagnosticRecommendations.CountAsync(dr => dr.DiagnosticId == diagnosticId));
    }
}
