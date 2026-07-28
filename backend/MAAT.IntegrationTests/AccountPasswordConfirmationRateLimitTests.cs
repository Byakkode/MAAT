using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 5/6 : POST /api/me/export et DELETE /api/me
// acceptent un mot de passe de confirmation — sans limitation de débit, ils formeraient
// un oracle de mot de passe hors du chemin /api/auth/login. Conteneur Postgres et
// WebApplicationFactory dédiés (pas AuthApiFixture) : AuthApiFixture fixe
// Jwt:AccessTokenLifetimeSeconds à 2 secondes pour le cas 8 (expiration rapide), ce qui
// périmerait le jeton en cours de séquence sur les six requêtes successives de ce test
// (chacune ~250 ms, dominée par la vérification bcrypt) et fausserait le résultat — un
// jeton expiré est rejeté par [Authorize] avant même d'atteindre le rate limiter, donc
// une partie des tentatives ne serait jamais comptée.
public class AccountPasswordConfirmationRateLimitTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";

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
                    ["Jwt:SigningKey"] = "password-confirmation-rate-limit-test-key-32b",
                    // Volontairement absent : Jwt:AccessTokenLifetimeSeconds garde sa
                    // valeur par défaut (900 s), largement suffisante pour six requêtes
                    // séquentielles.
                });
            });
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

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Test",
        sectorCode = "6201Z",
        sizeRange = "Micro",
        region = "Île-de-France",
    };

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<string> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Sixieme_tentative_de_confirmation_de_mot_de_passe_en_moins_de_15_minutes_repond_429()
    {
        var client = _factory.CreateClient();
        var accessToken = await RegisterCompanyAndLoginAdminAsync(client);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.SendAsync(
                AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = "MotDePasseErrone2026!" }));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Même politique nommée, même utilisateur : la sixième tentative compte dans le
        // même quota que les cinq précédentes, même en changeant d'endpoint — sans quoi
        // alterner entre les deux doublerait le nombre de mots de passe testables.
        var sixthResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = "MotDePasseErrone2026!" }));

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
    }
}
