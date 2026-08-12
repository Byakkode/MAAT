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

// docs/specs/recommandations.md, cas 1 à 11 et 13 à 21. Conteneur et jeton JWT dédiés,
// comme QuestionnaireApiFixture (voir son commentaire : jeton à 2 s d'AuthApiFixture trop
// court pour ces scénarios).
//
// Univers de questions et recommandations entièrement contrôlé par ce fixture, pas le
// référentiel réel (docs/specs/referentiel.md, 45 questions actives et en évolution) : les
// cas 6 à 8 dépendent d'un calcul de priorité exact (ρ_domaine, coût d'effort, départage),
// qui casserait si des recommandations réelles se déclenchaient aussi. Les 45
// questions/recommandations réelles sont donc désactivées juste après le seed
// (ReferenceDataIsolation) — seul le secteur "4941A" (sector-weights.csv) reste utilisé tel
// quel. Ce fixture ajoute une question de test par domaine (poids 1, pour des calculs de
// priorité simples à vérifier à la main) et un jeu de recommandations conçu pour isoler
// chaque facteur de la formule de priorisation (impact, ρ_domaine, coût d'effort) un par un.
public class RecommendationsApiFixture : IAsyncLifetime
{
    public const string EnvTestQuestionCode = "ENV-TEST01";
    public const string SocialQuestionCode = "SOC-TEST01";
    public const string EthicsQuestionCode = "ETH-TEST01";
    public const string ProcurementQuestionCode = "ACH-TEST01";
    public const string GovernanceQuestionCode = "GOU-TEST01";

    // Secteur seedé par SectorWeightConfiguration : Environnemental 0,400 / Social 0,200 /
    // Éthique 0,150 / Achats 0,150 / Gouvernance 0,100 — poids tous différents, nécessaire
    // pour le cas 6 (le domaine le plus lourd passe devant à impact et effort égaux).
    public const string SectorCode = "4941A";

    // Même impact (10) et même effort (Low), domaines différents (Environnemental 0,400 vs
    // Social 0,200) : cas 6.
    public const string EnvLowCode = "REC-ENV-LOW";
    public const string SocLowCode = "REC-SOC-LOW";

    // Même domaine (Environnemental) et même impact (10) que EnvLowCode, effort High au lieu
    // de Low : cas 7.
    public const string EnvHighCode = "REC-ENV-HIGH";

    // Même domaine (Gouvernance), même impact (5) et même effort (Medium) : priorités
    // strictement égales, départagées par code croissant (cas 8). Insérées dans le seed dans
    // l'ordre B puis A pour prouver que le tri final ne dépend pas de l'ordre d'insertion.
    public const string TieACode = "REC-TIE-A";
    public const string TieBCode = "REC-TIE-B";

    // Seuil de déclenchement volontairement large (5, donc toujours atteint si active) mais
    // is_active = false : ne doit jamais être retenue, quelle que soit la réponse (cas 4).
    public const string InactiveCode = "REC-INACTIVE";

    // Dédiée au cas 14 (désactivation *après* complétion, via mutation directe en base) :
    // ce fixture est un CollectionFixture partagé par toute la classe de test, donc une
    // recommandation que d'autres cas déclenchent aussi (ex. EnvLowCode) ne doit jamais être
    // désactivée en cours de suite, sous peine de casser tout test qui s'exécute après.
    public const string DeactivatableCode = "REC-DEACTIVATABLE";

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
                    ["Jwt:SigningKey"] = "recommendations-test-signing-key-32-bytes-min",
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // Les sector_weights "4941A"/"6202A" (cas 11) viennent de MAAT.Infrastructure/Seed/*.csv,
        // pas des migrations ; les questions et recommandations réelles sont désactivées
        // juste après (voir commentaire de classe).
        await new ReferenceDataSeeder(context).SeedAsync();
        await ReferenceDataIsolation.DeactivateAllAsync(context);

        context.Questions.AddRange(
            new Question(EnvTestQuestionCode, "Question environnementale de test.", RseDomain.Environmental, weight: 1m, displayOrder: 200),
            new Question(SocialQuestionCode, "Question sociale de test.", RseDomain.Social, weight: 1m, displayOrder: 201),
            new Question(EthicsQuestionCode, "Question éthique de test.", RseDomain.Ethics, weight: 1m, displayOrder: 202),
            new Question(ProcurementQuestionCode, "Question achats de test.", RseDomain.Procurement, weight: 1m, displayOrder: 203),
            new Question(GovernanceQuestionCode, "Question gouvernance de test.", RseDomain.Governance, weight: 1m, displayOrder: 204));

        context.Recommendations.AddRange(
            new Recommendation(EnvLowCode, RseDomain.Environmental, "Action environnementale, effort faible.", impactPoints: 10.00m, EffortLevel.Low, EnvTestQuestionCode, triggerMaxValue: 2),
            new Recommendation(SocLowCode, RseDomain.Social, "Action sociale, effort faible.", impactPoints: 10.00m, EffortLevel.Low, SocialQuestionCode, triggerMaxValue: 2),
            new Recommendation(EnvHighCode, RseDomain.Environmental, "Action environnementale, effort élevé.", impactPoints: 10.00m, EffortLevel.High, EnvTestQuestionCode, triggerMaxValue: 2),
            new Recommendation(TieBCode, RseDomain.Governance, "Action gouvernance, code B.", impactPoints: 5.00m, EffortLevel.Medium, GovernanceQuestionCode, triggerMaxValue: 2),
            new Recommendation(TieACode, RseDomain.Governance, "Action gouvernance, code A.", impactPoints: 5.00m, EffortLevel.Medium, GovernanceQuestionCode, triggerMaxValue: 2),
            new Recommendation(InactiveCode, RseDomain.Environmental, "Action jamais retenue.", impactPoints: 1.00m, EffortLevel.Low, EnvTestQuestionCode, triggerMaxValue: 5)
            {
                IsActive = false,
            },
            new Recommendation(DeactivatableCode, RseDomain.Environmental, "Action désactivée après coup.", impactPoints: 3.00m, EffortLevel.Low, EnvTestQuestionCode, triggerMaxValue: 2));

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

[CollectionDefinition("RecommendationsApi")]
public class RecommendationsApiCollection : ICollectionFixture<RecommendationsApiFixture>
{
    public const string Name = "RecommendationsApi";
}
