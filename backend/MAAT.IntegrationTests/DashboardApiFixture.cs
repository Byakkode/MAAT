using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/dashboard.md. Conteneur et jeton JWT dédiés, comme QuestionnaireApiFixture et
// RecommendationsApiFixture (voir leur commentaire, et CLAUDE.md : le jeton à 2 s
// d'AuthApiFixture n'existe que pour le cas 8 de auth-securite-rgpd.md). Les cas de benchmark
// (8 à 11) enregistrent plusieurs entreprises par test : un jeton à durée normale est d'autant
// plus nécessaire ici.
//
// QuestionConfiguration ne seed que 3 questions actives, toutes en Environnement
// (docs/specs/modele-donnees.md) : une question de test par domaine restant, poids 1, pour
// obtenir les cinq DomainScore qu'exige le tableau de bord (cas 3). Même principe pour les
// recommandations : les 3 réelles (REC-ENV-01/02/03) plus une par domaine restant, seuils
// tous à 2 et impacts distincts, pour disposer de sept recommandations déclenchables au total
// — nécessaire au cas 12 (cinq au maximum sur un total supérieur à cinq).
public class DashboardApiFixture : IAsyncLifetime
{
    public const string SocialQuestionCode = "SOC-TEST01";
    public const string EthicsQuestionCode = "ETH-TEST01";
    public const string ProcurementQuestionCode = "ACH-TEST01";
    public const string GovernanceQuestionCode = "GOU-TEST01";

    public const string RecSocialCode = "REC-SOC-TEST";
    public const string RecEthicsCode = "REC-ETH-TEST";
    public const string RecProcurementCode = "REC-ACH-TEST";
    public const string RecGovernanceCode = "REC-GOV-TEST";

    // display_order 200+ : jamais en collision avec ENV-01/02/03 (1 à 3, voir questions.csv).
    public static readonly IReadOnlyList<string> AllActiveQuestionCodes =
    [
        "ENV-01", "ENV-02", "ENV-03",
        SocialQuestionCode, EthicsQuestionCode, ProcurementQuestionCode, GovernanceQuestionCode,
    ];

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
                    ["Jwt:SigningKey"] = "dashboard-test-signing-key-32-bytes-minimum",
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01/02/03 et les sector_weights par défaut (0,200 par domaine) viennent de
        // MAAT.Infrastructure/Seed/*.csv, pas des migrations.
        await new ReferenceDataSeeder(context).SeedAsync();

        context.Questions.AddRange(
            new Question(SocialQuestionCode, "Question sociale de test.", RseDomain.Social, weight: 1m, displayOrder: 200),
            new Question(EthicsQuestionCode, "Question éthique de test.", RseDomain.Ethics, weight: 1m, displayOrder: 201),
            new Question(ProcurementQuestionCode, "Question achats de test.", RseDomain.Procurement, weight: 1m, displayOrder: 202),
            new Question(GovernanceQuestionCode, "Question gouvernance de test.", RseDomain.Governance, weight: 1m, displayOrder: 203));

        context.Recommendations.AddRange(
            new Recommendation(RecSocialCode, RseDomain.Social, "Action sociale de test.", impactPoints: 8.00m, EffortLevel.Low, SocialQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecEthicsCode, RseDomain.Ethics, "Action éthique de test.", impactPoints: 6.00m, EffortLevel.Low, EthicsQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecProcurementCode, RseDomain.Procurement, "Action achats de test.", impactPoints: 4.00m, EffortLevel.Low, ProcurementQuestionCode, triggerMaxValue: 2),
            new Recommendation(RecGovernanceCode, RseDomain.Governance, "Action gouvernance de test.", impactPoints: 2.00m, EffortLevel.Low, GovernanceQuestionCode, triggerMaxValue: 2));

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

[CollectionDefinition("DashboardApi")]
public class DashboardApiCollection : ICollectionFixture<DashboardApiFixture>
{
    public const string Name = "DashboardApi";
}
