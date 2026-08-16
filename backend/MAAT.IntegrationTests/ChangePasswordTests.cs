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

// Fixture dédiée à durée de vie de jeton par défaut, même raison que
// AccountPasswordConfirmationRateLimitTests (CLAUDE.md) : le scénario "les autres sessions
// sont invalidées, pas la session courante" enchaîne inscription + deux connexions + un
// changement de mot de passe, chacune un bcrypt (~250 ms) — un jeton à 2 s (AuthApiFixture)
// périmerait la session courante avant même l'appel qui la teste.
public class ChangePasswordTests : IAsyncLifetime
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string NewValidPassword = "NouveauMotDePasse2026!";

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
                    ["Jwt:SigningKey"] = "change-password-test-signing-key-32-bytes",
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

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

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

    private static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var header = Assert.Single(cookies, c => c.StartsWith($"{cookieName}=", StringComparison.Ordinal));
        var firstSegment = header.Split(';')[0];
        return firstSegment[(cookieName.Length + 1)..];
    }

    private async Task<(string AccessToken, string RefreshCookie)> RegisterAndLoginAsync(HttpClient client, string email, string password)
    {
        await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, password));
        return await LoginAsync(client, email, password);
    }

    private static async Task<(string AccessToken, string RefreshCookie)> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("accessToken").GetString()!, ExtractCookieValue(response, "refresh_token"));
    }

    private static HttpRequestMessage ChangePasswordRequest(string accessToken, string refreshCookie, string currentPassword, string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/me/password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Cookie", $"refresh_token={refreshCookie}");
        return request;
    }

    private static HttpRequestMessage RefreshRequest(string refreshCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={refreshCookie}");
        return request;
    }

    [Fact]
    public async Task Changement_reussi_invalide_les_autres_sessions_mais_conserve_la_session_courante()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (accessTokenA, refreshCookieA) = await RegisterAndLoginAsync(client, email, ValidPassword);
        var (_, refreshCookieB) = await LoginAsync(client, email, ValidPassword);

        var changeResponse = await client.SendAsync(ChangePasswordRequest(accessTokenA, refreshCookieA, ValidPassword, NewValidPassword));
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        var refreshWithCurrentSession = await client.SendAsync(RefreshRequest(refreshCookieA));
        Assert.Equal(HttpStatusCode.OK, refreshWithCurrentSession.StatusCode);

        var refreshWithOtherSession = await client.SendAsync(RefreshRequest(refreshCookieB));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithOtherSession.StatusCode);
    }

    [Fact]
    public async Task Connexion_echoue_avec_l_ancien_mot_de_passe_et_reussit_avec_le_nouveau_apres_changement()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (accessToken, refreshCookie) = await RegisterAndLoginAsync(client, email, ValidPassword);

        await client.SendAsync(ChangePasswordRequest(accessToken, refreshCookie, ValidPassword, NewValidPassword));

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = NewValidPassword });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Mot_de_passe_actuel_errone_repond_401_et_ne_change_rien()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (accessToken, refreshCookie) = await RegisterAndLoginAsync(client, email, ValidPassword);

        var response = await client.SendAsync(ChangePasswordRequest(accessToken, refreshCookie, "MauvaisMotDePasse2026!", NewValidPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var stillWorksWithOldPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, stillWorksWithOldPassword.StatusCode);
    }

    [Fact]
    public async Task Nouveau_mot_de_passe_trop_court_repond_400()
    {
        var client = CreateClient();
        var email = UniqueEmail();
        var (accessToken, refreshCookie) = await RegisterAndLoginAsync(client, email, ValidPassword);

        var response = await client.SendAsync(ChangePasswordRequest(accessToken, refreshCookie, ValidPassword, "Court1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
