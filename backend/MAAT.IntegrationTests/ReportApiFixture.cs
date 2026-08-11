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

// docs/specs/rapport-pdf.md, cas 1 à 9 et 13 à 20. Conteneur et jeton JWT dédiés, comme
// QuestionnaireApiFixture et RecommendationsApiFixture (voir leur commentaire) : chaque test
// enchaîne inscription + connexion + création + plusieurs réponses + complétion +
// génération(s) de rapport, largement au-delà des 2 s d'AuthApiFixture.
//
// Secteur "4941A" (seedé par sector-weights.csv : Environnemental 0,400 / Social 0,200 /
// Éthique 0,150 / Achats 0,150 / Gouvernance 0,100 — poids tous différents, nécessaire pour
// que la pondération sectorielle se voie réellement dans le rapport, cas 15/16). En plus des
// trois questions ENV-01/02/03 de référence (poids 3/2/1, mêmes valeurs que l'exemple de
// scoring.md), une question de test par domaine restant, poids 1, pour obtenir les cinq
// DomainScore qu'exige le rapport. Recommandations : les trois réelles (REC-ENV-01/02/03) plus
// une par domaine restant, seuils choisis pour que le jeu de réponses standard des tests de ce
// fichier (StandardAnswers) en déclenche cinq au total, de quoi peupler le plan d'actions sans
// qu'il soit vide.
public class ReportApiFixture : IAsyncLifetime
{
    public const string SectorCode = "4941A";

    public const string SocialQuestionCode = "SOC-TEST01";
    public const string EthicsQuestionCode = "ETH-TEST01";
    public const string ProcurementQuestionCode = "ACH-TEST01";
    public const string GovernanceQuestionCode = "GOU-TEST01";

    public const string RecSocialCode = "REC-SOC-TEST";
    public const string RecEthicsCode = "REC-ETH-TEST";
    public const string RecProcurementCode = "REC-ACH-TEST";
    public const string RecGovernanceCode = "REC-GOV-TEST";

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
                    ["Jwt:SigningKey"] = "report-api-test-signing-key-32-bytes-minimum",
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01/02/03, REC-ENV-01/02/03 et le secteur "4941A" viennent de
        // MAAT.Infrastructure/Seed/*.csv, pas des migrations.
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

[CollectionDefinition("ReportApi")]
public class ReportApiCollection : ICollectionFixture<ReportApiFixture>
{
    public const string Name = "ReportApi";
}
