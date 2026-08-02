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

// Conteneur et jeton JWT dédiés — pas AuthApiFixture, qui fixe Jwt:AccessTokenLifetimeSeconds
// à 2 s pour le cas 8 de auth-securite-rgpd.md : compléter un questionnaire enchaîne assez de
// requêtes authentifiées (création, plusieurs réponses, complétion) pour risquer de dépasser
// 2 s de façon intermittente. Voir AccountPasswordConfirmationRateLimitTests pour le précédent
// exact qui a motivé cette isolation.
//
// QuestionConfiguration ne seed que 3 questions actives, toutes en Environnement
// (docs/specs/modele-donnees.md). Compléter un diagnostic exige de répondre à toutes les
// questions actives (section 6 de questionnaire.md), et le cas 11 exige cinq lignes
// DomainScore : il faut donc au moins une question active par domaine restant. Seedées ici
// plutôt que dans QuestionConfiguration, qui reste un jeu d'exemple à but de démonstration,
// pas un jeu de test.
public class QuestionnaireApiFixture : IAsyncLifetime
{
    public const string SocialQuestionCode = "SOC-TEST01";
    public const string EthicsQuestionCode = "ETH-TEST01";
    public const string ProcurementQuestionCode = "ACH-TEST01";
    public const string GovernanceQuestionCode = "GOU-TEST01";
    public const string InactiveQuestionCode = "GOU-TEST99";

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
                    ["Jwt:SigningKey"] = "questionnaire-test-signing-key-32-bytes-min",
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01/02/03 (référence) et les sector_weights "4941A"/"6202A" utilisés par
        // QuestionnaireTests viennent de MAAT.Infrastructure/Seed/*.csv, pas des migrations.
        await new ReferenceDataSeeder(context).SeedAsync();

        context.Questions.AddRange(
            new Question(SocialQuestionCode, "Question sociale de test.", RseDomain.Social, weight: 1m, displayOrder: 100),
            new Question(EthicsQuestionCode, "Question éthique de test.", RseDomain.Ethics, weight: 1m, displayOrder: 101),
            new Question(ProcurementQuestionCode, "Question achats de test.", RseDomain.Procurement, weight: 1m, displayOrder: 102),
            new Question(GovernanceQuestionCode, "Question gouvernance de test.", RseDomain.Governance, weight: 1m, displayOrder: 103),
            new Question(InactiveQuestionCode, "Question gouvernance inactive de test.", RseDomain.Governance, weight: 1m, displayOrder: 104)
            {
                IsActive = false,
            });
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

[CollectionDefinition("QuestionnaireApi")]
public class QuestionnaireApiCollection : ICollectionFixture<QuestionnaireApiFixture>
{
    public const string Name = "QuestionnaireApi";
}
