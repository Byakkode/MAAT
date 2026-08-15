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
// RecommendationSelectionFailureTests substituant IRecommendationEngine.
//
// Univers de questions et recommandations entièrement contrôlé par ce fixture, pas le
// référentiel réel (docs/specs/referentiel.md, 45 questions actives et en évolution) : les
// cas 13, 14, 16 et 22 attendent un score global exact ("50,3333…", tranche 50-69) dérivé de
// trois questions Environnement de poids 3,00/2,00/1,00 — mêmes valeurs que le cas 4 de
// scoring.md — qui casserait si le domaine comptait d'autres questions actives, réelles ou
// non. Les 45 questions/recommandations réelles sont donc désactivées juste après le seed
// (ReferenceDataIsolation) ; seul le secteur "4941A" (sector-weights.csv) reste utilisé tel
// quel. Trois questions de test remplacent ENV-01/02/03 avec les mêmes poids.
public class ReportContentApiFixture : IAsyncLifetime
{
    public const string SectorCode = ReportApiFixture.SectorCode;

    // Poids 3,00/2,00/1,00 : mêmes valeurs que le cas 4 de docs/specs/scoring.md, jamais
    // renommées sans recalculer les scores attendus de ReportContentTests (cas 13, 14, 16, 22).
    public const string EnvQuestionCodeWeight3 = "ENV-TEST01";
    public const string EnvQuestionCodeWeight2 = "ENV-TEST02";
    public const string EnvQuestionCodeWeight1 = "ENV-TEST03";

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
        await ReferenceDataIsolation.DeactivateAllAsync(context);

        context.Questions.AddRange(
            new Question(EnvQuestionCodeWeight3, "Question environnementale de test, poids 3.", RseDomain.Environmental, weight: 3m, displayOrder: 1),
            new Question(EnvQuestionCodeWeight2, "Question environnementale de test, poids 2.", RseDomain.Environmental, weight: 2m, displayOrder: 2),
            new Question(EnvQuestionCodeWeight1, "Question environnementale de test, poids 1.", RseDomain.Environmental, weight: 1m, displayOrder: 3),
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
