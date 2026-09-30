using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Geocoding;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/norme-volontaire.md, section 4 et cas 11 à 14 : déclarations, sites et complétude
// à travers l'API, contre un vrai PostgreSQL (colonnes jsonb et text[] comprises). Même
// fixture que PlanLimitsTests : les droits dépendent de l'offre, et le géocodeur y est un
// double (FakeGeocoder).
[Collection(PlanLimitsApiCollection.Name)]
public class VsmeApiTests(PlanLimitsApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";
    private const string SitesUrl = "/api/company/sites";

    private static readonly object CompleteStatement = new
    {
        reportingBasis = "Consolidated",
        legalForm = "SAS",
        totalAssetsEur = 2_400_000,
        primaryCountry = "France",
        employeeCountUnit = "Headcount",
        omittedDisclosures = new[] { "B11" },
        subsidiaries = new[] { new { name = "Filiale Est", registeredAddress = "2 rue du Port 67000 Strasbourg" }, new { name = "", registeredAddress = "" } },
        certifications = new[] { new { name = "ISO 14001", issuer = "AFNOR Certification", obtainedOn = "2024-03-12", rating = (string?)null } },
        hasPractices = true,
        hasPolicies = true,
        policiesPublic = false,
        hasFutureInitiatives = true,
        hasTargets = false,
        coveredTopics = new[] { "Workforce", "ClimateChange", "Workforce" },
        pollutionReportingApplicable = true,
        pollutants = new[] { new { name = "NOx", medium = "Air", quantity = 0.8, unit = "t" } },
        circularEconomyApplied = false,
        minimumWageMet = true,
    };

    private static readonly object CompleteIndicators = new
    {
        revenueEur = 3_100_000,
        energyConsumptionKwh = 180_000,
        scope1Tco2e = 21,
        scope2LocationTco2e = 6,
        waterWithdrawalM3 = 900,
        hazardousWasteTons = 0.4,
        nonHazardousWasteTons = 12,
        recyclingRatePct = 55,
        permanentEmployees = 22,
        temporaryEmployees = 3,
        femaleEmployees = 11,
        maleEmployees = 14,
        recordableAccidents = 1,
        hoursWorked = 38_000,
        workFatalities = 0,
        collectiveBargainingPct = 100,
        trainingHoursPerEmployee = 14,
    };

    private async Task<(Guid CompanyId, Guid UserId, string Email, string AccessToken)> RegisterAsync(HttpClient client, SubscriptionPlan plan)
    {
        var email = $"vsme-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = ValidPassword,
            companyName = "Entreprise Norme Volontaire",
            sectorCode = "6201Z",
            sizeRange = "Small",
            region = "Grand Est",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var accessToken = await LoginAsync(client, email);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        await using var context = fixture.CreateDbContext();
        await TestSubscriptions.SetPlanAsync(context, companyId, plan);
        return (companyId, userId, email, accessToken);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object Site(string name, string address, bool? sensitive = false) => new
    {
        name,
        address,
        tenure = "Leased",
        inOrNearSensitiveArea = sensitive,
        sensitiveAreaName = sensitive == true ? "Natura 2000 « Vosges du Nord »" : null,
    };

    // Cas 11.
    [Fact]
    public async Task Declaration_ouverte_des_Essential_refusee_en_Starter_et_au_Viewer()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, email, token) = await RegisterAsync(client, SubscriptionPlan.Starter);

        var starter = await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", token, CompleteStatement));
        Assert.Equal(HttpStatusCode.Forbidden, starter.StatusCode);
        var body = await starter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal("Essential", body.GetProperty("requiredPlan").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, token, Site("Siège", "1 rue de Paris")))).StatusCode);

        await using (var context = fixture.CreateDbContext())
        {
            await TestSubscriptions.SetPlanAsync(context, companyId, SubscriptionPlan.Essential);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", token, CompleteStatement))).StatusCode);

        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            user.Role = UserRole.Viewer;
            await context.SaveChangesAsync();
        }

        var viewerToken = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", viewerToken, CompleteStatement))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, viewerToken, Site("Siège", "1 rue de Paris")))).StatusCode);
        // Cas 12 : le Viewer ne saisit pas non plus d'indicateurs, mais il lit tout.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Put, "/api/indicators/2025", viewerToken, CompleteIndicators))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, "/api/vsme/2025", viewerToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, "/api/vsme/2025/completeness", viewerToken))).StatusCode);
    }

    [Fact]
    public async Task Declaration_relue_telle_qu_enregistree_listes_comprises()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Authorized(HttpMethod.Get, "/api/vsme/2025", token))).StatusCode);
        await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", token, CompleteStatement)));

        var saved = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, "/api/vsme/2025", token)));

        Assert.Equal("Consolidated", saved.GetProperty("reportingBasis").GetString());
        Assert.Equal(["B11"], saved.GetProperty("omittedDisclosures").EnumerateArray().Select(e => e.GetString()));
        // Ligne vide ignorée, thèmes dédoublonnés et triés dans l'ordre de la norme.
        Assert.Equal(1, saved.GetProperty("subsidiaries").GetArrayLength());
        Assert.Equal(["ClimateChange", "Workforce"], saved.GetProperty("coveredTopics").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("2024-03-12", saved.GetProperty("certifications")[0].GetProperty("obtainedOn").GetString());
        Assert.Equal("Air", saved.GetProperty("pollutants")[0].GetProperty("medium").GetString());
    }

    [Fact]
    public async Task B1_ne_peut_pas_etre_omise_et_un_lien_doit_etre_une_adresse_web()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var omitB1 = await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", token, new { omittedDisclosures = new[] { "B1" } }));
        var badUrl = await client.SendAsync(Authorized(HttpMethod.Put, "/api/vsme/2025", token, new { pollutionReportingApplicable = true, pollutionReportUrl = "javascript:alert(1)" }));

        Assert.Equal(HttpStatusCode.BadRequest, omitB1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badUrl.StatusCode);
    }

    // Cas 13.
    [Fact]
    public async Task Site_geocode_a_l_enregistrement_et_enregistre_meme_sans_coordonnees()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var located = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, token, Site("Siège", "12 rue de la Paix 75002 Paris"))));
        var unknown = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, token, Site("Dépôt", "Lieu-dit introuvable"))));

        Assert.True(located.GetProperty("geocoded").GetBoolean());
        Assert.Equal(48.8686, located.GetProperty("latitude").GetDouble());
        Assert.False(unknown.GetProperty("geocoded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("latitude").ValueKind);

        // Corriger l'adresse relance le géocodage ; ne rien changer à une adresse localisée, non.
        var siteId = unknown.GetProperty("id").GetGuid();
        var fixedSite = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, $"{SitesUrl}/{siteId}", token, Site("Dépôt", "3 quai des Chartrons 33000 Bordeaux"))));
        Assert.True(fixedSite.GetProperty("geocoded").GetBoolean());

        var callsBefore = fixture.Geocoder.Calls;
        var locatedId = located.GetProperty("id").GetGuid();
        await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, $"{SitesUrl}/{locatedId}", token, Site("Siège social", "12 rue de la Paix 75002 Paris"))));
        Assert.Equal(callsBefore, fixture.Geocoder.Calls);

        var list = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, SitesUrl, token)));
        Assert.Equal(["Siège social", "Dépôt"], list.EnumerateArray().Select(s => s.GetProperty("name").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Authorized(HttpMethod.Delete, $"{SitesUrl}/{locatedId}", token))).StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, SitesUrl, token)))).GetArrayLength());
    }

    [Fact]
    public async Task Site_d_une_autre_entreprise_introuvable()
    {
        var client = fixture.CreateClient();
        var (_, _, _, ownerToken) = await RegisterAsync(client, SubscriptionPlan.Essential);
        var (_, _, _, otherToken) = await RegisterAsync(client, SubscriptionPlan.Essential);
        var site = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, ownerToken, Site("Siège", "1 rue de Paris"))));
        var siteId = site.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authorized(HttpMethod.Put, $"{SitesUrl}/{siteId}", otherToken, Site("Piraté", "1 rue de Paris")))).StatusCode);
        await client.SendAsync(Authorized(HttpMethod.Delete, $"{SitesUrl}/{siteId}", otherToken));
        Assert.Equal(1, (await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, SitesUrl, ownerToken)))).GetArrayLength());
        Assert.Equal(0, (await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, SitesUrl, otherToken)))).GetArrayLength());
    }

    // Cas 14.
    [Fact]
    public async Task Cinquante_et_unieme_site_refuse()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        await using (var context = fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < 50; i++)
            {
                context.CompanySites.Add(new MAAT.Domain.Entities.CompanySite(companyId,
                    new MAAT.Domain.Entities.CompanySiteDetails($"Site {i}", $"{i} rue de Lyon", SiteTenure.Leased, false, null), now));
            }

            await context.SaveChangesAsync();
        }

        var response = await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, token, Site("Site 51", "51 rue de Lyon")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // Cas 17 côté API : une entreprise qui renseigne tout atteint 11/11, et la complétude est
    // celle que reçoit le rapport.
    [Fact]
    public async Task Completude_atteint_onze_sur_onze_et_rapport_conforme()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);
        var year = DateTimeOffset.UtcNow.Year;

        var empty = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, $"/api/vsme/{year}/completeness", token)));
        Assert.False(empty.GetProperty("isCompliant").GetBoolean());

        await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, $"/api/indicators/{year}", token, CompleteIndicators)));
        await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, $"/api/vsme/{year}", token, CompleteStatement)));
        await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Post, SitesUrl, token, Site("Siège", "5 place Kléber 67000 Strasbourg", sensitive: true))));

        var complete = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Get, $"/api/vsme/{year}/completeness", token)));

        Assert.True(complete.GetProperty("isCompliant").GetBoolean(), complete.ToString());
        Assert.Equal(11, complete.GetProperty("completeCount").GetInt32());
        Assert.Equal("Omitted", complete.GetProperty("disclosures")[10].GetProperty("state").GetString());
    }

    // Règles de cohérence (cas 8) à travers l'API : le total suit ses parties.
    [Fact]
    public async Task Total_des_emissions_recalcule_a_partir_des_scopes()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var saved = await ReadJsonAsync(await client.SendAsync(Authorized(HttpMethod.Put, "/api/indicators/2025", token,
            new { co2EmissionsTons = 999, scope1Tco2e = 12.5, scope2LocationTco2e = 4 })));

        Assert.Equal(16.5, saved.GetProperty("co2EmissionsTons").GetDouble());
    }

    [Fact]
    public async Task Indicateur_negatif_refuse()
    {
        var client = fixture.CreateClient();
        var (_, _, _, token) = await RegisterAsync(client, SubscriptionPlan.Essential);

        var response = await client.SendAsync(Authorized(HttpMethod.Put, "/api/indicators/2025", token, new { scope1Tco2e = -3 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ADR 0013 : lecture de la réponse GeoJSON de la Géoplateforme, sans réseau.
    [Fact]
    public void Reponse_de_la_Geoplateforme_lue_et_resultat_peu_sur_ecarte()
    {
        static GeocodedAddress? Parse(string json) => GeoplateformeGeocoder.Parse(JsonDocument.Parse(json).RootElement);

        var found = Parse("""{"type":"FeatureCollection","features":[{"geometry":{"type":"Point","coordinates":[2.330831,48.868632]},"properties":{"label":"12 Rue de la Paix 75002 Paris","score":0.97}}]}""");
        var weak = Parse("""{"features":[{"geometry":{"coordinates":[2.3,48.8]},"properties":{"label":"Paris","score":0.31}}]}""");
        var none = Parse("""{"type":"FeatureCollection","features":[]}""");

        Assert.Equal(new GeocodedAddress(48.868632, 2.330831, "12 Rue de la Paix 75002 Paris"), found);
        Assert.Null(weak);
        Assert.Null(none);
    }
}
