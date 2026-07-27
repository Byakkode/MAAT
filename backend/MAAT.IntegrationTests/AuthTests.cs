using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

[Collection(AuthApiCollection.Name)]
public class AuthTests(AuthApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    // Doit figurer dans la liste locale des mots de passe compromis (>= 12 caractères).
    private const string CompromisedPassword = "123456789012";

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

    private async Task<(string Email, string AccessToken, string RefreshCookie)> RegisterAndLoginAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;
        var refreshCookie = ExtractCookieValue(loginResponse, "refresh_token");

        return (email, accessToken, refreshCookie);
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string? accessToken = null, string? refreshCookie = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (accessToken is not null)
        {
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }

        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"refresh_token={refreshCookie}");
        }

        return request;
    }

    // ---- Authentification ----

    [Fact]
    public async Task Cas1_Inscription_valide_cree_company_et_user_admin()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var db = fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email.ToLowerInvariant());
        Assert.Equal(UserRole.Admin, user.Role);

        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == user.CompanyId);
        Assert.Equal("Entreprise Test", company.Name);
    }

    [Fact]
    public async Task Cas2_Inscription_email_existant_repond_comme_succes_sans_creer_de_compte()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        var payload = RegisterPayload(email, ValidPassword);

        var first = await client.PostAsJsonAsync("/api/auth/register", payload);
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await client.PostAsJsonAsync("/api/auth/register", payload);
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(firstBody, secondBody);

        await using var db = fixture.CreateDbContext();
        var count = await db.Users.AsNoTracking().CountAsync(u => u.Email == email.ToLowerInvariant());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Cas3_Mot_de_passe_trop_court_repond_400()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(UniqueEmail(), "Court1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cas4_Mot_de_passe_compromis_repond_400()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(UniqueEmail(), CompromisedPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cas5_Connexion_valide_retourne_access_token_et_cookie_refresh_conforme()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = Assert.Single(cookies, c => c.StartsWith("refresh_token=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cas6_Email_inconnu_et_mot_de_passe_errone_repondent_de_maniere_identique()
    {
        var client = fixture.CreateClient();
        var knownEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(knownEmail, ValidPassword));

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = knownEmail, password = "MotDePasseErrone2026!" });
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = UniqueEmail(), password = "MotDePasseErrone2026!" });

        Assert.Equal(wrongPasswordResponse.StatusCode, unknownEmailResponse.StatusCode);
        Assert.Equal(
            await wrongPasswordResponse.Content.ReadAsStringAsync(),
            await unknownEmailResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cas7_Sixieme_tentative_en_moins_de_15_minutes_repond_429()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "MotDePasseErrone2026!" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "MotDePasseErrone2026!" });

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
    }

    // ---- Jetons ----

    [Fact]
    public async Task Cas8_Access_token_expire_repond_401()
    {
        var client = fixture.CreateClient();
        var (_, accessToken, _) = await RegisterAndLoginAsync(client);

        await Task.Delay(TimeSpan.FromSeconds(3));

        var request = AuthorizedRequest(HttpMethod.Get, "/api/auth/me", accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cas9_Refresh_valide_emet_un_nouveau_couple_et_revoque_l_ancien()
    {
        var client = fixture.CreateClient();
        var (_, _, refreshCookie1) = await RegisterAndLoginAsync(client);

        var refreshRequest = AuthorizedRequest(HttpMethod.Post, "/api/auth/refresh", refreshCookie: refreshCookie1);
        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshCookie2 = ExtractCookieValue(refreshResponse, "refresh_token");
        Assert.NotEqual(refreshCookie1, refreshCookie2);

        await using var db = fixture.CreateDbContext();
        var hash1 = MAAT.Infrastructure.Security.TokenHasher.Sha256Hex(refreshCookie1);
        var revokedToken = await db.RefreshTokens.AsNoTracking().SingleAsync(rt => rt.TokenHash == hash1);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    [Fact]
    public async Task Cas10_Refresh_deja_revoque_reutilise_revoque_toute_la_famille()
    {
        var client = fixture.CreateClient();
        var (_, _, refreshCookie1) = await RegisterAndLoginAsync(client);

        var firstRefresh = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/auth/refresh", refreshCookie: refreshCookie1));
        var refreshCookie2 = ExtractCookieValue(firstRefresh, "refresh_token");

        var reuseResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/auth/refresh", refreshCookie: refreshCookie1));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var secondTokenNowRevokedResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/auth/refresh", refreshCookie: refreshCookie2));
        Assert.Equal(HttpStatusCode.Unauthorized, secondTokenNowRevokedResponse.StatusCode);
    }

    [Fact]
    public async Task Cas11_Jwt_a_signature_alteree_repond_401()
    {
        var client = fixture.CreateClient();
        var (_, accessToken, _) = await RegisterAndLoginAsync(client);

        var lastChar = accessToken[^1];
        var tamperedChar = lastChar == 'A' ? 'B' : 'A';
        var tamperedToken = accessToken[..^1] + tamperedChar;

        var request = AuthorizedRequest(HttpMethod.Get, "/api/auth/me", tamperedToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cas12_Jwt_ne_contient_que_les_claims_autorises()
    {
        var client = fixture.CreateClient();
        var (email, accessToken, _) = await RegisterAndLoginAsync(client);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var claimTypes = jwt.Claims.Select(c => c.Type).ToHashSet();

        Assert.Contains("sub", claimTypes);
        Assert.Contains("company_id", claimTypes);
        Assert.Contains("role", claimTypes);
        Assert.Contains("jti", claimTypes);
        Assert.Contains("exp", claimTypes);
        Assert.Contains("iat", claimTypes);

        Assert.DoesNotContain("email", claimTypes);
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains(email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains("Entreprise Test", StringComparison.OrdinalIgnoreCase));
    }
}
