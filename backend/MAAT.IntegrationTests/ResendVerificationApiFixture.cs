using MAAT.Application.Interfaces;
using MAAT.Infrastructure.Persistence;
using MAAT.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// Fixture dédiée plutôt que AuthApiFixture (docs/specs, CLAUDE.md) : deux raisons distinctes de
// diverger. D'abord, une durée de vie de jeton par défaut — AuthApiFixture la réduit à 2 s pour
// le seul cas d'expiration ; aucun test ici ne s'appuie sur un access token (register/verify/
// resend sont tous anonymes), mais mieux vaut ne pas hériter d'une contrainte de timing propre
// à un autre fichier de tests. Ensuite, et surtout, IEmailSender doit être remplacé par
// CapturingEmailSender pour récupérer le jeton en clair envoyé par e-mail (la base ne stocke
// que son hash) — AuthApiFixture reste sur LoggingEmailSender, partagé par AuthTests.cs, qu'il
// serait risqué de modifier pour un seul nouveau fichier de tests.
public class ResendVerificationApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private WebApplicationFactory<Program> _factory = default!;

    public CapturingEmailSender EmailSender { get; } = new();

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
                    ["Jwt:SigningKey"] = "resend-verification-test-signing-key-32-bytes",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IEmailSender>(EmailSender));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
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

[CollectionDefinition(Name)]
public class ResendVerificationApiCollection : ICollectionFixture<ResendVerificationApiFixture>
{
    public const string Name = "ResendVerificationApi";
}
