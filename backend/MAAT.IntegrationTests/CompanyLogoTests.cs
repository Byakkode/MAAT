using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise, à travers l'API. Même fixture
// que PlanLimitsTests : le droit dépend de l'offre, et CapturingReportGenerator montre ce que
// DiagnosticService transmet au rapport.
[Collection(PlanLimitsApiCollection.Name)]
public class CompanyLogoTests(PlanLimitsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string LogoUrl = "/api/company/logo";

    private async Task<(Guid CompanyId, Guid UserId, string Email, string AccessToken)> RegisterAsync(HttpClient client, SubscriptionPlan plan)
    {
        var email = $"logo-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = ValidPassword,
            companyName = "Entreprise Logo",
            sectorCode = "4941A",
            sizeRange = "Micro",
            region = "Île-de-France",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var accessToken = await LoginAsync(client, email);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        await SetPlanAsync(companyId, plan);
        return (companyId, userId, email, accessToken);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private async Task SetPlanAsync(Guid companyId, SubscriptionPlan plan)
    {
        await using var context = fixture.CreateDbContext();
        await TestSubscriptions.SetPlanAsync(context, companyId, plan);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        return request;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string token, byte[] bytes, string fileName = "logo.png", string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };

        var request = Authorized(HttpMethod.Put, LogoUrl, token);
        request.Content = form;
        return client.SendAsync(request);
    }

    internal static byte[] MakeImage(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColor.Parse("#1565FF"));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    [Fact]
    public async Task Starter_EnvoiRefuse_AvecLOffreRequise()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Starter);

        var response = await UploadAsync(client, token, MakeImage(200, 100));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal("Essential", body.GetProperty("requiredPlan").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).StatusCode);
    }

    [Fact]
    public async Task Essential_EnvoiPuisLecture_ImagePngNonMiseEnCache()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var upload = await UploadAsync(client, token, MakeImage(320, 120, SKEncodedImageFormat.Jpeg), "logo.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var get = await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        Assert.True(get.Headers.CacheControl?.NoStore);

        // Réencodé en PNG, à ses dimensions d'origine (sous la limite de 600 px).
        using var codec = SKCodec.Create(new MemoryStream(await get.Content.ReadAsByteArrayAsync()));
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(320, codec.Info.Width);
        Assert.Equal(120, codec.Info.Height);
    }

    [Fact]
    public async Task GrandeImage_ReduiteA600PixelsSurSonPlusGrandCote()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, token, MakeImage(3000, 1000))).StatusCode);

        var bytes = await (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).Content.ReadAsByteArrayAsync();
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        Assert.Equal(600, codec.Info.Width);
        Assert.Equal(200, codec.Info.Height);
    }

    // Le type annoncé et l'extension ne prouvent rien : seul le décodage fait foi.
    [Fact]
    public async Task FichierQuiNEstPasUneImage_400_RienNEstEnregistre()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var response = await UploadAsync(client, token, "<svg onload=alert(1)>"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
        Assert.Contains("PNG, JPEG ou WebP", message);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).StatusCode);
    }

    [Fact]
    public async Task FichierDePlusDe2Mo_400()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var response = await UploadAsync(client, token, new byte[2 * 1024 * 1024 + 1]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_EnvoiEtSuppressionRefuses_LectureAutorisee()
    {
        var client = fixture.CreateClient();
        var (_, userId, email, adminToken) = await RegisterAsync(client, SubscriptionPlan.Essential);
        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, adminToken, MakeImage(200, 100))).StatusCode);

        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            user.Role = UserRole.Viewer;
            await context.SaveChangesAsync();
        }

        var viewerToken = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, viewerToken, MakeImage(200, 100))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Delete, LogoUrl, viewerToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, viewerToken))).StatusCode);
    }

    [Fact]
    public async Task Suppression_Idempotente()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, token, MakeImage(200, 100))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Authorized(HttpMethod.Delete, LogoUrl, token))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Authorized(HttpMethod.Delete, LogoUrl, token))).StatusCode);
    }

    // Le logo suit l'offre effective : transmis au rapport en Essential, plus en Starter — mais
    // conservé, et de nouveau utilisé si l'entreprise se réabonne.
    [Fact]
    public async Task Rapport_LogoTransmisSelonLOffre_ConserveApresRetourAStarter()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            user.EmailVerified = true;
            await context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, token, MakeImage(200, 100))).StatusCode);
        var stored = await (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).Content.ReadAsByteArrayAsync();

        var create = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/diagnostics")
        {
            Headers = { { "Authorization", $"Bearer {token}" } },
            Content = JsonContent.Create(new { }),
        });
        var diagnosticId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, defaultValue: 3);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token))).StatusCode);
        var reportUrl = $"/api/diagnostics/{diagnosticId}/report";

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, reportUrl, token))).StatusCode);
        Assert.Equal(stored, fixture.ReportGenerator.LastData!.CompanyLogoPng);

        await SetPlanAsync(companyId, SubscriptionPlan.Starter);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, reportUrl, token))).StatusCode);
        Assert.Null(fixture.ReportGenerator.LastData!.CompanyLogoPng);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, LogoUrl, token))).StatusCode);

        await SetPlanAsync(companyId, SubscriptionPlan.Essential);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, reportUrl, token))).StatusCode);
        Assert.Equal(stored, fixture.ReportGenerator.LastData!.CompanyLogoPng);
    }

    // Droit à l'effacement (auth-securite-rgpd.md, section 6) : le logo part avec l'entreprise.
    [Fact]
    public async Task SuppressionDuCompte_SupprimeLeLogo()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, token, MakeImage(200, 100))).StatusCode);

        var delete = Authorized(HttpMethod.Delete, "/api/me", token);
        delete.Content = JsonContent.Create(new { password = ValidPassword });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.CompanyLogos.AnyAsync(l => l.CompanyId == companyId));
    }
}
