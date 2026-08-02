using MAAT.Domain.Services;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// Conteneur dédié plutôt que PostgresFixture (collection "Postgres" partagée par de
// nombreux autres tests) : DemoDataSeeder insère des entreprises et des diagnostics
// persistants, ce qui polluerait les autres tests de la collection s'il tournait contre la
// même base (voir RemoveReferenceDataSeedTests, même choix pour la même raison).
public class DemoDataSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private MaatDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MaatDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new MaatDbContext(options);
    }

    private static IHostEnvironment DevelopmentEnvironment()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        return environment;
    }

    private static IHostEnvironment ProductionEnvironment()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        return environment;
    }

    [Fact]
    public async Task Seed_demo_cree_trois_entreprises_avec_des_diagnostics_completes_a_des_dates_differentes()
    {
        await using var context = CreateContext();
        var seeder = new DemoDataSeeder(context, new ScoringService(), DevelopmentEnvironment());

        var seeded = await seeder.SeedAsync();

        Assert.True(seeded);

        var companies = await context.Companies
            .Join(context.Diagnostics, c => c.Id, d => d.CompanyId, (c, d) => new { c.Name, d.CreatedAt, d.CompletedAt, d.Status, d.GlobalScore })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(3, companies.Count);
        Assert.All(companies, c => Assert.Equal(Domain.Enums.DiagnosticStatus.Completed, c.Status));
        Assert.Equal(3, companies.Select(c => c.CompletedAt).Distinct().Count());

        // Profil faible < moyen < mature (voir DemoDataSeeder, ordonné par date de création).
        Assert.True(companies[0].GlobalScore < companies[1].GlobalScore);
        Assert.True(companies[1].GlobalScore < companies[2].GlobalScore);
        Assert.True(companies[0].GlobalScore is >= 0 and < 35);
        Assert.True(companies[1].GlobalScore is >= 45 and < 65);
        Assert.True(companies[2].GlobalScore is >= 75);
    }

    [Fact]
    public async Task Seed_demo_insere_45_questions_toutes_inactives()
    {
        await using var context = CreateContext();
        var seeder = new DemoDataSeeder(context, new ScoringService(), DevelopmentEnvironment());

        await seeder.SeedAsync();

        var demoQuestions = await context.Questions.Where(q => q.Code.StartsWith("DEMO-")).ToListAsync();

        Assert.Equal(45, demoQuestions.Count);
        // is_active=false : jamais reprises par QuestionRepository.FindAllActiveAsync, donc
        // jamais visibles dans un questionnaire réel (voir DemoDataSeeder, note d'isolation).
        Assert.All(demoQuestions, q => Assert.False(q.IsActive));
    }

    [Fact]
    public async Task Seed_demo_est_idempotent()
    {
        await using var context = CreateContext();
        var seeder = new DemoDataSeeder(context, new ScoringService(), DevelopmentEnvironment());

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(3, await context.Companies.CountAsync());
        Assert.Equal(3, await context.Diagnostics.CountAsync());
        Assert.Equal(45, await context.Questions.CountAsync(q => q.Code.StartsWith("DEMO-")));
    }

    [Fact]
    public async Task Seed_demo_refuse_de_s_executer_hors_environnement_Development()
    {
        await using var context = CreateContext();
        var seeder = new DemoDataSeeder(context, new ScoringService(), ProductionEnvironment());

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());

        Assert.Equal(0, await context.Companies.CountAsync());
    }
}
