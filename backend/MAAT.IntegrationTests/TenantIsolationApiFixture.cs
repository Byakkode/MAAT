using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 4, cas 13 à 17. Conteneur et jeton JWT dédiés,
// comme QuestionnaireApiFixture et RecommendationsApiFixture (voir leur commentaire) : le
// jeton à 2 s d'AuthApiFixture existe pour le cas 8 (expiration réelle sans attendre 15
// minutes), pas pour ces cas-ci. TenantIsolationTests enchaîne deux inscriptions + connexions
// (donc quatre passages bcrypt coût 12) avant de réutiliser le premier jeton pour vérifier le
// cloisonnement — sous charge (plusieurs conteneurs Postgres démarrés en parallèle par
// d'autres fixtures), ce jeton pouvait expirer avant d'être réutilisé et l'API répondait alors
// 401 (jeton expiré, ClockSkew=Zero) plutôt que le 404 attendu, sans que le cloisonnement ne
// soit réellement mis en défaut. Un rattachement à AuthApiFixture partagerait le même piège.
public class TenantIsolationApiFixture : IAsyncLifetime
{
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
                    ["Jwt:SigningKey"] = "tenant-isolation-test-signing-key-32-bytes-min",
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01 (référencée par Cas14) vient de MAAT.Infrastructure/Seed/questions.csv, pas
        // des migrations.
        await new ReferenceDataSeeder(context).SeedAsync();
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

[CollectionDefinition("TenantIsolationApi")]
public class TenantIsolationApiCollection : ICollectionFixture<TenantIsolationApiFixture>
{
    public const string Name = "TenantIsolationApi";
}
