using System.Net;
using System.Net.Http.Json;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 1 : cas 21 à 25, renvoi de l'e-mail de
// vérification. Fixture dédiée (ResendVerificationApiFixture) pour intercepter le jeton en
// clair via CapturingEmailSender — la base ne conserve que son hash.
[Collection(ResendVerificationApiCollection.Name)]
public class ResendVerificationTests(ResendVerificationApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

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

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Cas21_Renvoi_vers_adresse_inconnue_repond_comme_une_adresse_connue_non_verifiee()
    {
        var client = fixture.CreateClient();
        var knownEmail = UniqueEmail();
        var unknownEmail = UniqueEmail();
        await RegisterAsync(client, knownEmail);

        var knownResponse = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = knownEmail });
        var knownBody = await knownResponse.Content.ReadAsStringAsync();

        var unknownResponse = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = unknownEmail });
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();

        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(knownBody, unknownBody);

        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Users.AsNoTracking().AnyAsync(u => u.Email == unknownEmail.ToLowerInvariant()));
    }

    [Fact]
    public async Task Cas22_Renvoi_vers_adresse_deja_verifiee_repond_pareil_mais_n_emet_aucun_nouveau_jeton()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var registrationToken = Assert.Single(fixture.EmailSender.TokensFor(email));
        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = registrationToken });
        Assert.Equal(HttpStatusCode.NoContent, verifyResponse.StatusCode);

        var resendResponse = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resendResponse.StatusCode);
        // Toujours un seul e-mail envoyé pour cette adresse (celui de l'inscription) : le
        // renvoi n'a émis aucun nouveau jeton pour un compte déjà vérifié.
        Assert.Single(fixture.EmailSender.TokensFor(email));
    }

    [Fact]
    public async Task Cas23_Renvoi_emet_un_nouveau_jeton_et_invalide_l_ancien()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var oldToken = Assert.Single(fixture.EmailSender.TokensFor(email));

        var resendResponse = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resendResponse.StatusCode);

        var tokensAfterResend = fixture.EmailSender.TokensFor(email);
        Assert.Equal(2, tokensAfterResend.Count);
        var newToken = tokensAfterResend[^1];
        Assert.NotEqual(oldToken, newToken);

        var verifyWithOldToken = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = oldToken });
        Assert.Equal(HttpStatusCode.BadRequest, verifyWithOldToken.StatusCode);

        await using var db = fixture.CreateDbContext();
        var oldHash = TokenHasher.Sha256Hex(oldToken);
        var oldRow = await db.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.TokenHash == oldHash);
        Assert.NotNull(oldRow.ConsumedAt);
    }

    [Fact]
    public async Task Cas24_Verification_avec_le_nouveau_jeton_apres_renvoi_reussit()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        await client.PostAsJsonAsync("/api/auth/resend-verification", new { email });
        var newToken = fixture.EmailSender.TokensFor(email)[^1];

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = newToken });

        Assert.Equal(HttpStatusCode.NoContent, verifyResponse.StatusCode);

        await using var db = fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email.ToLowerInvariant());
        Assert.True(user.EmailVerified);
    }

    [Fact]
    public async Task Cas25_Sixieme_renvoi_en_moins_de_15_minutes_pour_la_meme_adresse_repond_429()
    {
        var client = fixture.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email });
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var sixthResponse = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);
    }
}
