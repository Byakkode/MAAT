using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MAAT.Application.DTOs;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Pdf;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 12 à 18. IReportGenerator substitué par
// CapturingReportGenerator (voir son commentaire et celui de ReportContentApiFixture) :
// l'assertion porte sur le ReportData réellement assemblé par DiagnosticService à travers
// tout le chemin HTTP réel (auth, préconditions, dépôts, service), sans dépendre d'une
// extraction de texte PDF.
[Collection(ReportContentApiCollection.Name)]
public class ReportContentTests(ReportContentApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    private static readonly IReadOnlyDictionary<string, int> StandardAnswers = new Dictionary<string, int>
    {
        [ReportContentApiFixture.EnvQuestionCodeWeight3] = 5,
        [ReportContentApiFixture.EnvQuestionCodeWeight2] = 2,
        [ReportContentApiFixture.EnvQuestionCodeWeight1] = 0,
        [ReportContentApiFixture.SocialQuestionCode] = 4,
        [ReportContentApiFixture.EthicsQuestionCode] = 2,
        [ReportContentApiFixture.ProcurementQuestionCode] = 1,
        [ReportContentApiFixture.GovernanceQuestionCode] = 0,
    };

    private static readonly IReadOnlyDictionary<string, int> NoTriggerAnswers = new Dictionary<string, int>
    {
        [ReportContentApiFixture.EnvQuestionCodeWeight3] = 5,
        [ReportContentApiFixture.EnvQuestionCodeWeight2] = 5,
        [ReportContentApiFixture.EnvQuestionCodeWeight1] = 5,
        [ReportContentApiFixture.SocialQuestionCode] = 5,
        [ReportContentApiFixture.EthicsQuestionCode] = 5,
        [ReportContentApiFixture.ProcurementQuestionCode] = 5,
        [ReportContentApiFixture.GovernanceQuestionCode] = 5,
    };

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static object RegisterPayload(string email, string password) => new
    {
        email,
        password,
        companyName = "Entreprise Contenu Test",
        sectorCode = ReportContentApiFixture.SectorCode,
        sizeRange = "Small",
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

    private async Task<(Guid UserId, string AccessToken)> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);
        return (userId, accessToken);
    }

    private async Task VerifyEmailDirectlyAsync(Guid userId)
    {
        await using var context = fixture.CreateDbContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        user.EmailVerified = true;
        await context.SaveChangesAsync();
    }

    private async Task<Guid> CompleteDiagnosticAsync(HttpClient client, string token, IReadOnlyDictionary<string, int> answers)
    {
        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", token, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var diagnosticId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // docs/specs/referentiel.md : jamais une liste de codes codée en dur — voir
        // ReportTests.CompleteDiagnosticAsync pour le même principe.
        await DiagnosticQuestionAnswering.AnswerActiveQuestionsAsync(client, token, diagnosticId, overrides: answers);

        var completeResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, $"/api/diagnostics/{diagnosticId}/complete", token));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        return diagnosticId;
    }

    private async Task<(Guid DiagnosticId, string AccessToken)> CompleteVerifiedDiagnosticAsync(HttpClient client, IReadOnlyDictionary<string, int> answers)
    {
        var (userId, token) = await RegisterCompanyAndLoginAdminAsync(client);
        await VerifyEmailDirectlyAsync(userId);
        var diagnosticId = await CompleteDiagnosticAsync(client, token, answers);
        return (diagnosticId, token);
    }

    [Fact]
    public async Task Cas13_Score_global_affiche_correspond_au_score_persiste_arrondi()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        var diagnostic = await context.Diagnostics.SingleAsync(d => d.Id == diagnosticId);

        var data = fixture.ReportGenerator.LastData!;
        Assert.Equal(diagnostic.GlobalScore, data.GlobalScore);
        // Scénario standard (ENV 5/2/0, SOC 4, ETH 2, ACH 1, GOU 0, secteur 4941A) : score
        // global 50,3333…, affiché 50 (MidpointRounding.AwayFromZero).
        Assert.Equal(50, ScoringService.RoundForDisplay(data.GlobalScore));
    }

    [Fact]
    public async Task Cas14_Libelle_qualitatif_est_celui_de_la_tranche_50_69()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Démarche structurée", fixture.ReportGenerator.LastData!.GlobalScoreLabel);
    }

    [Fact]
    public async Task Cas15_Les_cinq_domaines_figurent_avec_leur_score_et_sector_weight_persistes()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        var persisted = await context.DomainScores.Where(ds => ds.DiagnosticId == diagnosticId).ToListAsync();
        Assert.Equal(5, persisted.Count);

        var data = fixture.ReportGenerator.LastData!;
        Assert.Equal(5, data.DomainScores.Count);

        foreach (var domainScore in persisted)
        {
            var reported = data.DomainScores.Single(d => d.Domain == domainScore.Domain);
            Assert.Equal(domainScore.Score, reported.Score);
            Assert.Equal(domainScore.SectorWeight, reported.SectorWeight);
            Assert.Equal(domainScore.Numerator, reported.Numerator);
            Assert.Equal(domainScore.Denominator, reported.Denominator);
        }
    }

    [Fact]
    public async Task Cas16_La_somme_des_contributions_est_coherente_avec_le_score_global()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = fixture.ReportGenerator.LastData!;
        var sumOfContributions = data.DomainScores.Sum(d => d.Contribution);

        // Écart borné, pas une égalité stricte : Score et SectorWeight sont chacun persistés
        // arrondis (numeric(5,2) et numeric(4,3)) alors que GlobalScore a été calculé en pleine
        // précision avant son propre arrondi de persistance — deux arrondis indépendants
        // produisent un résidu de quelques millièmes, pas une divergence.
        Assert.True(
            Math.Abs(sumOfContributions - data.GlobalScore) < 0.05m,
            $"Somme des contributions ({sumOfContributions}) incohérente avec le score global ({data.GlobalScore}).");
    }

    // docs/specs/rapport-pdf.md, cas 22 : la valeur « Transparence » n'existe que si le
    // lecteur peut refaire le calcul avec les deux nombres imprimés sur la ligne et retomber
    // sur le score affiché — contrairement au cas 16 (cohérence globale, écart toléré), c'est
    // ici une égalité exacte entre deux valeurs déjà arrondies pour l'affichage : aucune
    // marge n'est recevable, une égalité approchée ne serait pas vérifiable à la main.
    [Fact]
    public async Task Cas22_Numerateur_divise_par_denominateur_redonne_le_score_affiche_sur_la_meme_ligne()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = fixture.ReportGenerator.LastData!;
        Assert.NotEmpty(data.DomainScores);

        foreach (var domainScore in data.DomainScores)
        {
            var displayedScore = ScoringService.RoundForDisplay(domainScore.Score);
            var recomputedFromPrintedValues = ScoringService.RoundForDisplay(domainScore.Numerator / domainScore.Denominator * 100m);

            Assert.True(
                displayedScore == recomputedFromPrintedValues,
                $"{domainScore.Domain} : {domainScore.Numerator} ÷ {domainScore.Denominator} × 100 arrondi vaut " +
                $"{recomputedFromPrintedValues}, mais le score affiché sur la même ligne est {displayedScore}.");
        }
    }

    [Fact]
    public async Task Cas17_Le_bloc_de_mentions_contient_exactement_la_reserve_exigee()
    {
        const string expected =
            "Ce rapport résulte d'une auto-évaluation déclarative réalisée par l'entreprise sur la " +
            "plateforme MAAT. Il ne constitue ni une certification, ni un audit, ni une notation par " +
            "un organisme tiers indépendant.";

        Assert.Equal(expected, QuestPdfReportGenerator.MentionsText);
    }

    [Fact]
    public async Task Cas18_Diagnostic_sans_recommandation_declenchee_document_valide_plan_vide()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, NoTriggerAnswers);

        await using (var context = fixture.CreateDbContext())
        {
            Assert.Equal(0, await context.DiagnosticRecommendations.CountAsync(dr => dr.DiagnosticId == diagnosticId));
        }

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = fixture.ReportGenerator.LastData!;
        Assert.Empty(data.Recommendations);
        Assert.Equal(0, data.TotalRecommendationCount);

        // « Document valide » : le générateur réel (pas la capture ci-dessus) ne doit pas
        // lever avec un plan d'actions vide, et doit produire un PDF non trivial — c'est la
        // branche qui remplace le tableau par un message positif (recommandations.md, section
        // 4 : « aucune recommandation déclenchée » est un succès, pas une erreur).
        var bytes = new QuestPdfReportGenerator().Generate(data);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public async Task Cas12_Ordre_du_document_suit_priority_rank_persiste_pas_un_recalcul()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        // Inverse directement en base les deux premiers rangs de priorité, simulant un état
        // qu'aucun recalcul n'aurait produit : l'ordre du rapport doit suivre ce qui est
        // persisté, pas relancer RecommendationEngine.Prioritize (recommandations.md, cas 11,
        // encaissé ici pour le rapport PDF).
        await using (var context = fixture.CreateDbContext())
        {
            var entries = await context.DiagnosticRecommendations
                .Where(dr => dr.DiagnosticId == diagnosticId)
                .OrderBy(dr => dr.PriorityRank)
                .ToListAsync();
            Assert.True(entries.Count >= 2, "Le scénario standard doit déclencher au moins deux recommandations.");

            (entries[0].PriorityRank, entries[1].PriorityRank) = (entries[1].PriorityRank, entries[0].PriorityRank);
            await context.SaveChangesAsync();
        }

        List<string> expectedActionTextOrder;
        await using (var context = fixture.CreateDbContext())
        {
            expectedActionTextOrder = await (
                from dr in context.DiagnosticRecommendations
                where dr.DiagnosticId == diagnosticId
                orderby dr.PriorityRank
                join r in context.Recommendations on dr.RecommendationId equals r.Id
                select r.ActionText).ToListAsync();
        }

        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actualOrder = fixture.ReportGenerator.LastData!.Recommendations.Select(r => r.ActionText).ToList();
        Assert.Equal(expectedActionTextOrder, actualOrder);
    }

    private async Task<ReportData> GenerateReportAsync(HttpClient client, string token, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/report", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return fixture.ReportGenerator.LastData!;
    }

    private static async Task<List<string>> GetRecommendationCodesAsync(HttpClient client, string token, Guid diagnosticId)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/recommendations", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Select(r => r.GetProperty("code").GetString()!)
            .ToList();
    }

    // Cas 28 : le plan d'actions du rapport reflète le suivi saisi à l'écran Plan d'actions
    // (statut, responsable, échéance) et la case cochée depuis le tableau de bord — les deux
    // chemins qui marquent une action comme terminée aboutissent au même statut.
    [Fact]
    public async Task Cas28_Le_suivi_du_plan_d_actions_figure_dans_le_rapport()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);
        var codes = await GetRecommendationCodesAsync(client, token, diagnosticId);
        Assert.True(codes.Count >= 3, "Le scénario standard doit déclencher au moins trois recommandations.");

        var dueDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var tracked = await client.SendAsync(AuthorizedRequest(HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/action-plan/{codes[0]}", token,
            new { status = "InProgress", assignedTo = "Claire Martin", dueDate, notes = "Note interne, jamais imprimée." }));
        Assert.Equal(HttpStatusCode.OK, tracked.StatusCode);

        var checkedFromDashboard = await client.SendAsync(AuthorizedRequest(HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/recommendations/{codes[1]}", token,
            new { isCompleted = true }));
        Assert.Equal(HttpStatusCode.OK, checkedFromDashboard.StatusCode);

        var blocked = await client.SendAsync(AuthorizedRequest(HttpMethod.Patch, $"/api/diagnostics/{diagnosticId}/action-plan/{codes[2]}", token,
            new { status = "Blocked" }));
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        var data = await GenerateReportAsync(client, token, diagnosticId);

        // Même ordre que GET /recommendations : priority_rank persisté (cas 12).
        Assert.Equal(ActionItemStatus.InProgress, data.Recommendations[0].Status);
        Assert.Equal("Claire Martin", data.Recommendations[0].AssignedTo);
        Assert.Equal(dueDate, data.Recommendations[0].DueDate);
        Assert.Equal(ActionItemStatus.Done, data.Recommendations[1].Status);
        Assert.Equal(ActionItemStatus.Blocked, data.Recommendations[2].Status);
        Assert.All(data.Recommendations.Skip(3), r => Assert.Equal(ActionItemStatus.Planned, r.Status));

        Assert.Equal(new ReportActionStatusSummary(codes.Count - 3, 1, 1, 1), data.ActionStatusSummary);
        Assert.Equal(data.TotalRecommendationCount, data.ActionStatusSummary.Total);
    }

    // Cas 29 : année de référence des indicateurs = la plus récente qui ne dépasse pas l'année
    // de complétion ; l'année précédente sert à la tendance ; une année postérieure est ignorée.
    [Fact]
    public async Task Cas29_Indicateurs_de_l_annee_de_reference_et_de_l_annee_precedente()
    {
        var client = fixture.CreateClient();
        var (diagnosticId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var withoutIndicators = await GenerateReportAsync(client, token, diagnosticId);
        Assert.Null(withoutIndicators.Indicators);

        var completionYear = withoutIndicators.CompletedAt.Year;
        foreach (var (year, co2) in new[] { (completionYear - 2, 120.0), (completionYear - 1, 100.0), (completionYear + 1, 999.0) })
        {
            var put = await client.SendAsync(AuthorizedRequest(HttpMethod.Put, $"/api/indicators/{year}", token, new { co2EmissionsTons = co2 }));
            Assert.True(put.IsSuccessStatusCode, $"PUT /api/indicators/{year} : {put.StatusCode}");
        }

        var data = await GenerateReportAsync(client, token, diagnosticId);

        Assert.NotNull(data.Indicators);
        Assert.Equal(completionYear - 1, data.Indicators!.Year);
        Assert.Equal(completionYear - 2, data.Indicators.PreviousYear);
        var co2Item = data.Indicators.Items.Single(i => i.Label == "Émissions CO₂");
        Assert.Equal(100.0, co2Item.Value);
        Assert.Equal(120.0, co2Item.PreviousValue);
        Assert.All(data.Indicators.Items.Where(i => i != co2Item), i => Assert.Null(i.Value));
    }

    // Cas 30 : l'historique s'arrête au diagnostic du rapport — un diagnostic complété plus
    // tard ne modifie pas un rapport déjà émis (section 3) — et le rapport du second
    // diagnostic porte les scores par domaine du premier pour l'écart.
    [Fact]
    public async Task Cas30_Historique_borne_au_diagnostic_du_rapport()
    {
        var client = fixture.CreateClient();
        var (firstId, token) = await CompleteVerifiedDiagnosticAsync(client, StandardAnswers);

        var first = await GenerateReportAsync(client, token, firstId);
        Assert.Single(first.History);
        Assert.Null(first.PreviousDomainScores);

        var secondId = await CompleteDiagnosticAsync(client, token, NoTriggerAnswers);
        var second = await GenerateReportAsync(client, token, secondId);

        Assert.Equal(2, second.History.Count);
        Assert.Equal(first.GlobalScore, second.History[0].GlobalScore);
        Assert.Equal(second.GlobalScore, second.History[1].GlobalScore);
        Assert.NotNull(second.PreviousDomainScores);
        Assert.Equal(first.DomainScores.ToDictionary(d => d.Domain, d => d.Score), second.PreviousDomainScores);

        var firstAgain = await GenerateReportAsync(client, token, firstId);
        Assert.Single(firstAgain.History);
        Assert.Null(firstAgain.PreviousDomainScores);
    }
}
