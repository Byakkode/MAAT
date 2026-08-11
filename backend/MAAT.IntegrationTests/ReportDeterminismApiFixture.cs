using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 10 et 11 — le générateur réel (QuestPdfReportGenerator),
// mais avec TimeProvider substitué par une horloge figée : sans cela, deux appels HTTP
// réellement séparés dans le temps embarqueraient des DocumentMetadata.CreationDate/
// ModifiedDate différentes (voir le commentaire de QuestPdfReportGenerator sur ce piège) et
// casseraient l'égalité d'octets attendue par construction, indépendamment de tout bug réel.
public class ReportDeterminismApiFixture : IAsyncLifetime
{
    public const string SectorCode = ReportApiFixture.SectorCode;

    public static readonly DateTimeOffset FixedNow = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    public const string SocialQuestionCode = ReportApiFixture.SocialQuestionCode;
    public const string EthicsQuestionCode = ReportApiFixture.EthicsQuestionCode;
    public const string ProcurementQuestionCode = ReportApiFixture.ProcurementQuestionCode;
    public const string GovernanceQuestionCode = ReportApiFixture.GovernanceQuestionCode;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
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
                    ["Jwt:SigningKey"] = "report-determinism-test-signing-key-32-bytes",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        await new ReferenceDataSeeder(context).SeedAsync();

        context.Questions.AddRange(
            new Question(SocialQuestionCode, "Question sociale de test.", RseDomain.Social, weight: 1m, displayOrder: 200),
            new Question(EthicsQuestionCode, "Question éthique de test.", RseDomain.Ethics, weight: 1m, displayOrder: 201),
            new Question(ProcurementQuestionCode, "Question achats de test.", RseDomain.Procurement, weight: 1m, displayOrder: 202),
            new Question(GovernanceQuestionCode, "Question gouvernance de test.", RseDomain.Governance, weight: 1m, displayOrder: 203));

        await context.SaveChangesAsync();
    }

    public HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public MaatDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MaatDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new MaatDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("ReportDeterminismApi")]
public class ReportDeterminismApiCollection : ICollectionFixture<ReportDeterminismApiFixture>
{
    public const string Name = "ReportDeterminismApi";
}
