using MAAT.Application.Interfaces;
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

// docs/specs/rapport-pdf.md, cas 12 à 18 : IReportGenerator substitué par
// CapturingReportGenerator (voir son commentaire), même principe que
// RecommendationSelectionFailureTests substituant IRecommendationEngine. Même seed que
// ReportApiFixture — voir son commentaire pour le détail du secteur et des questions/
// recommandations de test.
public class ReportContentApiFixture : IAsyncLifetime
{
    public const string SectorCode = ReportApiFixture.SectorCode;

    public const string SocialQuestionCode = ReportApiFixture.SocialQuestionCode;
    public const string EthicsQuestionCode = ReportApiFixture.EthicsQuestionCode;
    public const string ProcurementQuestionCode = ReportApiFixture.ProcurementQuestionCode;
    public const string GovernanceQuestionCode = ReportApiFixture.GovernanceQuestionCode;

    public const string RecSocialCode = ReportApiFixture.RecSocialCode;
    public const string RecEthicsCode = ReportApiFixture.RecEthicsCode;
    public const string RecProcurementCode = ReportApiFixture.RecProcurementCode;
    public const string RecGovernanceCode = ReportApiFixture.RecGovernanceCode;

    public readonly CapturingReportGenerator ReportGenerator = new();

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
                    ["Jwt:SigningKey"] = "report-content-test-signing-key-32-bytes-min",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReportGenerator>();
                services.AddSingleton<IReportGenerator>(ReportGenerator);
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

        context.Recommendations.AddRange(
            new Recommendation(RecSocialCode, RseDomain.Social, "Action sociale de test.", impactPoints: 5.00m, EffortLevel.Low, SocialQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecEthicsCode, RseDomain.Ethics, "Action éthique de test.", impactPoints: 5.00m, EffortLevel.Low, EthicsQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecProcurementCode, RseDomain.Procurement, "Action achats de test.", impactPoints: 5.00m, EffortLevel.Low, ProcurementQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecGovernanceCode, RseDomain.Governance, "Action gouvernance de test.", impactPoints: 5.00m, EffortLevel.Low, GovernanceQuestionCode, triggerMaxValue: 2));

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

[CollectionDefinition("ReportContentApi")]
public class ReportContentApiCollection : ICollectionFixture<ReportContentApiFixture>
{
    public const string Name = "ReportContentApi";
}
