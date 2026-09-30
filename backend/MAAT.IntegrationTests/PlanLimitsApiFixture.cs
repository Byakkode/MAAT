using MAAT.Application.Interfaces;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Seed;
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

// docs/specs/abonnement.md, section 8 (cas 19 à 31). Référentiel réel : répondre 0 partout
// déclenche les 45 recommandations du seed, assez pour vérifier les limites de 3 et 12.
// IReportGenerator remplacé par CapturingReportGenerator pour lire ce que DiagnosticService
// transmet au rapport selon l'offre ; IPaymentGateway par FakePaymentGateway, comme
// BillingApiFixture ; IGeocoder par FakeGeocoder et ISensitiveAreaLocator par FakeSensitiveAreaLocator (ADR 0013 et
// 0014, aucun appel réseau). Durée de vie
// de jeton par défaut (CLAUDE.md).
public class PlanLimitsApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private WebApplicationFactory<Program> _factory = default!;

    public CapturingReportGenerator ReportGenerator { get; } = new();

    public FakeGeocoder Geocoder { get; } = new();

    public FakeSensitiveAreaLocator SensitiveAreas { get; } = new();

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
                    ["Jwt:SigningKey"] = "plan-limits-test-signing-key-32-bytes-minimum",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IPaymentGateway>(new FakePaymentGateway()));
                services.Replace(ServiceDescriptor.Singleton<IReportGenerator>(ReportGenerator));
                services.RemoveAll<IGeocoder>();
                services.AddSingleton<IGeocoder>(Geocoder);
                services.RemoveAll<ISensitiveAreaLocator>();
                services.AddSingleton<ISensitiveAreaLocator>(SensitiveAreas);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
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

[CollectionDefinition(Name)]
public class PlanLimitsApiCollection : ICollectionFixture<PlanLimitsApiFixture>
{
    public const string Name = "PlanLimitsApi";
}
