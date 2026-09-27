using System.Collections.Generic;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// Jetons d'accès de 2 secondes : réservé au cas 8 de auth-securite-rgpd.md (expiration d'un
// jeton, AuthTests). Tout autre test passe par une fixture à durée de vie par défaut
// (CLAUDE.md) — voir AccountApiFixture ci-dessous.
public class AuthApiFixture : IAsyncLifetime
{
    // null : durée de vie par défaut de l'application (Jwt:AccessTokenLifetimeSeconds absent).
    protected virtual string? AccessTokenLifetimeSeconds => "2";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Explicite plutôt qu'implicite : LoggingEmailSender n'est enregistré
            // qu'en Development (voir Program.cs et EmailSenderStartupTests).
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                    ["Jwt:SigningKey"] = "integration-test-signing-key-32-bytes-minimum-xyz",
                };
                if (AccessTokenLifetimeSeconds is { } lifetime)
                {
                    settings["Jwt:AccessTokenLifetimeSeconds"] = lifetime;
                }

                config.AddInMemoryCollection(settings);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
        // ENV-01 (référencée par AccountRgpdTests et TenantIsolationTests) vient de
        // MAAT.Infrastructure/Seed/questions.csv, pas des migrations.
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

[CollectionDefinition("AuthApi")]
public class AuthApiCollection : ICollectionFixture<AuthApiFixture>
{
    public const string Name = "AuthApi";
}

// Même base et mêmes données de référence qu'AuthApiFixture, avec la durée de vie de jeton par
// défaut. AccountRgpdTests enchaîne inscription, 45 réponses, ajout d'un compte (bcrypt) puis
// DELETE /api/me avec le premier jeton : avec des jetons de 2 secondes, cette requête arrivait
// souvent après expiration (401), ce qui masquait le comportement testé.
public class AccountApiFixture : AuthApiFixture
{
    protected override string? AccessTokenLifetimeSeconds => null;
}

[CollectionDefinition(Name)]
public class AccountApiCollection : ICollectionFixture<AccountApiFixture>
{
    public const string Name = "AccountApi";
}
