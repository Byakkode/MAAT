using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

// docs/specs/questionnaire.md, cas 15 — le plus important de la liste : un échec du calcul
// de score doit annuler toute la transaction et laisser le diagnostic InProgress. Ce cas
// teste la transaction de DiagnosticService.CompleteAsync, PAS le moteur de scoring : les
// conditions qui pouvaient auparavant faire échouer le calcul (une pondération sectorielle
// incomplète) sont désormais rejetées en base par une contrainte différée sur
// sector_weights (migration AddSectorWeightCoverageConstraint) — il n'est plus possible de
// construire cet état par de simples insertions. IScoringService est donc substitué par un
// faux qui lève, pour isoler la garantie transactionnelle testée ici de ce qui peut, en
// pratique, faire échouer le calcul lui-même.
//
// Conteneur et jeton JWT dédiés, comme QuestionnaireApiFixture : pas AuthApiFixture (jeton
// à 2 s, cf. son commentaire).
public class QuestionnaireScoringFailureTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private readonly IScoringService _failingScoringService = Substitute.For<IScoringService>();
    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _failingScoringService
            .CalculateScore(Arg.Any<IReadOnlyList<QuestionScoreInput>>(), Arg.Any<IReadOnlyDictionary<RseDomain, decimal>>())
            .Throws(new SectorWeightNotFoundException(RseDomain.Governance));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                    ["Jwt:SigningKey"] = "scoring-failure-test-signing-key-32-bytes-min",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IScoringService>();
                services.AddSingleton(_failingScoringService);
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
    public async Task Cas15_Echec_du_calcul_annule_la_transaction_et_laisse_le_diagnostic_InProgress()
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

        // IScoringService substitué lève quel que soit son entrée : peu importe la valeur
        // répondue, mais toutes les questions actives doivent l'être pour que la complétion
        // dépasse le contrôle de complétude et atteigne réellement le service substitué
        // (docs/specs/referentiel.md : jamais une liste de codes codée en dur).
        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, defaultValue: 3);

        var completeResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));

        Assert.False(
            completeResponse.IsSuccessStatusCode,
            "La complétion aurait dû échouer : IScoringService substitué lève systématiquement.");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();

        var diagnostic = await context.Diagnostics.SingleAsync(d => d.Id == diagnosticId);
        Assert.Equal(DiagnosticStatus.InProgress, diagnostic.Status);
        Assert.Null(diagnostic.GlobalScore);
        Assert.Null(diagnostic.CompletedAt);

        Assert.Equal(0, await context.DomainScores.CountAsync(ds => ds.DiagnosticId == diagnosticId));
    }
}
