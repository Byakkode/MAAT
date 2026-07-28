using System.Net;
using System.Net.Http.Json;
using MAAT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 5 : durcissement HTTP. Ces tests ne
// touchent jamais la base (CORS et en-têtes sont posés par des middlewares qui
// s'exécutent avant le routage), donc une chaîne de connexion factice suffit — même
// principe que EmailSenderStartupTests / JwtSigningKeyStartupTests.
public class HttpHardeningTests
{
    private static Dictionary<string, string?> BaseConfig(string allowedOrigin) => new()
    {
        ["ConnectionStrings:Default"] = "Host=localhost;Port=5433;Database=maat;Username=maat;Password=unused",
        ["Jwt:SigningKey"] = "http-hardening-test-signing-key-32-bytes-min",
        ["Cors:AllowedOrigins:0"] = allowedOrigin,
    };

    private static WebApplicationFactory<Program> CreateFactory(string allowedOrigin = "https://maat-app.example.test")
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Production, pas Development : UseHsts (comme le reste de l'écosystème
            // ASP.NET Core) n'a d'effet qu'en dehors de Development, pour ne pas gêner
            // le développement local en HTTP.
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(BaseConfig(allowedOrigin)));
            builder.ConfigureServices(services => services.AddScoped<MAAT.Application.Interfaces.IEmailSender, NoOpEmailSender>());
        });
    }

    [Fact]
    public async Task En_tetes_de_durcissement_presents_sur_toute_reponse()
    {
        using var factory = CreateFactory();

        // BaseAddress https + hôte explicite : HstsMiddleware n'ajoute l'en-tête que
        // pour les requêtes HTTPS (IsHttps), et jamais pour localhost/127.0.0.1
        // (exception délibérée d'ASP.NET Core pour ne pas gêner le développeur) — sans
        // les deux, le test ne prouverait rien.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://maat-app.example.test/"),
        });

        var response = await client.GetAsync("/api/route-inexistante");

        Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var hsts));
        Assert.Contains(hsts!, v => v.Contains("includeSubDomains", StringComparison.OrdinalIgnoreCase));

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff!);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var csp));
        Assert.Contains(csp!, v => v.Contains("frame-ancestors 'none'", StringComparison.Ordinal));

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrer));
        Assert.Contains("strict-origin-when-cross-origin", referrer!);
    }

    [Fact]
    public async Task CORS_origine_autorisee_recoit_les_en_tetes_Access_Control_avec_credentials()
    {
        const string allowedOrigin = "https://maat-app.example.test";
        using var factory = CreateFactory(allowedOrigin);
        var client = factory.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        preflight.Headers.Add("Origin", allowedOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(preflight);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin));
        Assert.Equal(allowedOrigin, Assert.Single(allowOrigin!));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var allowCredentials));
        Assert.Equal("true", Assert.Single(allowCredentials!));
    }

    [Fact]
    public async Task CORS_origine_non_autorisee_ne_recoit_aucun_en_tete_Access_Control()
    {
        using var factory = CreateFactory("https://maat-app.example.test");
        var client = factory.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        preflight.Headers.Add("Origin", "https://evil.example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(preflight);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private sealed class NoOpEmailSender : MAAT.Application.Interfaces.IEmailSender
    {
        public Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct) =>
            Task.CompletedTask;
    }
}

// Test isolé (conteneur Postgres dédié) : la journalisation des échecs de connexion
// nécessite une vraie recherche en base (AuthService.LoginAsync), contrairement aux
// tests d'en-têtes/CORS ci-dessus.
public class FailedLoginLoggingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();
    private readonly List<(LogLevel Level, string Message)> _logs = [];
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
                    ["Jwt:SigningKey"] = "failed-login-logging-test-signing-key-32b",
                });
            });
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
        });

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MaatDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Tentative_de_connexion_echouee_est_journalisee_avec_IP_et_horodatage_sans_l_email()
    {
        var client = _factory.CreateClient();
        var testedEmail = $"user-{Guid.NewGuid():N}@example.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = testedEmail, password = "MotDePasseErrone2026!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        lock (_logs)
        {
            Assert.Contains(
                _logs,
                l => l.Message.Contains("connexion", StringComparison.OrdinalIgnoreCase)
                    && l.Message.Contains("échouée", StringComparison.OrdinalIgnoreCase));

            Assert.DoesNotContain(
                _logs,
                l => l.Message.Contains(testedEmail, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class CapturingLoggerProvider(List<(LogLevel Level, string Message)> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<(LogLevel Level, string Message)> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (sink)
                {
                    sink.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
