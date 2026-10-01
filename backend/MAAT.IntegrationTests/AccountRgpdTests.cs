using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 6 : cas de test 18 à 20. La purge doit
// couvrir les neuf tables de la chaîne documentée (EmailVerificationToken incluse) et
// laisser les tables de référence (Question, Recommendation, SectorWeight) intactes.
[Collection(AccountApiCollection.Name)]
public class AccountRgpdTests(AccountApiFixture fixture)
{
    private const string ValidPassword = "MotDePasseValide2026!";

    // Id non figé : ENV-01 vient de MAAT.Infrastructure/Seed/questions.csv, chargé par
    // ReferenceDataSeeder avec un Guid généré à l'insertion — plus une constante de
    // migration comme avant, donc résolu à l'exécution ci-dessous.
    private const string SeededQuestionCode = "ENV-01";

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

    private async Task<(Guid CompanyId, Guid UserId, string Email, string AccessToken)> RegisterCompanyAndLoginAdminAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, ValidPassword));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var companyId = Guid.Parse(jwt.Claims.Single(c => c.Type == "company_id").Value);
        var userId = Guid.Parse(jwt.Claims.Single(c => c.Type == "sub").Value);

        return (companyId, userId, email, accessToken);
    }

    private static async Task<Guid> CreateDiagnosticAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", accessToken, new { }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    // Suivi d'une action, une ligne de son historique, un ticket de support et un logo : les
    // données saisies par l'entreprise hors du diagnostic lui-même.
    private async Task<(Guid ProgressId, Guid ChangeId, Guid TicketId)> SeedActionsTicketAndLogoAsync(
        Guid companyId, Guid userId, Guid diagnosticId, byte[] logo)
    {
        await using var context = fixture.CreateDbContext();
        var progress = ActionItemProgress.Create(diagnosticId, "ENV-REC-01");
        progress.Update(ActionItemStatus.InProgress, "Responsable QSE", new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), "Devis demandé.");
        var change = new ActionItemChange(
            diagnosticId, "ENV-REC-01", new ActionItemFieldChange(ActionItemField.Status, "Planned", "InProgress"), userId, DateTimeOffset.UtcNow);
        var ticket = new SupportTicket(
            companyId, userId, 42, "https://github.com/exemple/support/issues/42", "Export PDF vide", "Le rapport s'ouvre sans page de garde.", "bug");

        context.ActionItemProgresses.Add(progress);
        context.ActionItemChanges.Add(change);
        context.SupportTickets.Add(ticket);
        context.CompanyLogos.Add(new CompanyLogo(companyId, logo, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        return (progress.Id, change.Id, ticket.Id);
    }

    // Seede un jeu complet de données company-scopées (une ligne par table de la chaîne
    // de purge, sauf User/Company déjà créés par l'inscription) pour que le test vérifie
    // une vraie purge, pas l'absence de données qui n'auraient jamais existé.
    //
    // is_completed=true et completed_at renseigné sur la DiagnosticRecommendation : ce
    // sont des champs saisis par l'utilisateur (case cochée dans le tableau de bord),
    // pas dérivés — le cas 18 doit vérifier qu'ils survivent dans l'export, pas
    // seulement que la ligne existe.
    private async Task<(Guid DiagnosticId, Guid ResponseId, Guid ReportId, Guid RecommendationId, DateTimeOffset RecommendationCompletedAt)> SeedCompanyDataAsync(
        HttpClient client, string accessToken, Guid userId)
    {
        var createResponse = await client.SendAsync(AuthorizedRequest(HttpMethod.Post, "/api/diagnostics", accessToken, new { }));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var diagnosticId = created.GetProperty("id").GetGuid();

        Guid responseId;
        Guid reportId;
        Guid recommendationId;
        var recommendationCompletedAt = DateTimeOffset.UtcNow;
        await using (var context = fixture.CreateDbContext())
        {
            var seededQuestionId = await context.Questions.Where(q => q.Code == SeededQuestionCode).Select(q => q.Id).SingleAsync();
            var response = new Response(diagnosticId, seededQuestionId, 3);
            var domainScore = new DomainScore(diagnosticId, RseDomain.Environmental, 60m, 0.3m, 180m, 300m);
            var report = new Report(diagnosticId, userId);
            var recommendation = new Recommendation(
                $"REC-{Guid.NewGuid():N}"[..20],
                RseDomain.Environmental,
                "Mettre en place un suivi des émissions.",
                impactPoints: 5m,
                effortLevel: EffortLevel.Medium,
                triggerQuestionCode: SeededQuestionCode,
                triggerMaxValue: 2);
            var diagnosticRecommendation = new DiagnosticRecommendation(diagnosticId, recommendation.Id, priorityRank: 1)
            {
                IsCompleted = true,
                CompletedAt = recommendationCompletedAt,
            };

            context.Responses.Add(response);
            context.DomainScores.Add(domainScore);
            context.Reports.Add(report);
            context.Recommendations.Add(recommendation);
            context.DiagnosticRecommendations.Add(diagnosticRecommendation);
            await context.SaveChangesAsync();

            responseId = response.Id;
            reportId = report.Id;
            recommendationId = recommendation.Id;
        }

        return (diagnosticId, responseId, reportId, recommendationId, recommendationCompletedAt);
    }

    // Inscrit un compte séparé (pour un mot de passe haché par le vrai pipeline bcrypt, pas un
    // hachage manuel dans le test) puis le rattache à l'entreprise cible avec le rôle demandé
    // directement en base : POST /api/users/invite n'est pas utilisable ici, il crée un compte
    // sans mot de passe exploitable (auth-securite-rgpd.md, section 4). L'entreprise solo créée
    // par l'inscription est ensuite retirée, orpheline de tout compte.
    private async Task<(Guid UserId, string Email, string AccessToken)> AddUserToCompanyAsync(
        HttpClient client, Guid companyId, UserRole role, string password = ValidPassword)
    {
        var email = UniqueEmail();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, password));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        Guid userId;
        Guid orphanCompanyId;
        await using (var context = fixture.CreateDbContext())
        {
            var user = await context.Users.SingleAsync(u => u.Email == email.ToLowerInvariant());
            userId = user.Id;
            orphanCompanyId = user.CompanyId;
            user.CompanyId = companyId;
            user.Role = role;
            await context.SaveChangesAsync();
            await context.Companies.Where(c => c.Id == orphanCompanyId).ExecuteDeleteAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        return (userId, email, body.GetProperty("accessToken").GetString()!);
    }

    // docs/specs/coquille-et-compte.md, section 6 : la portée de DELETE /api/me dépend du rôle
    // et du nombre d'administrateurs restants — vérifiée ici sur des entreprises à plusieurs
    // comptes, contrairement au reste de cette classe (RegisterCompanyAndLoginAdminAsync ne crée
    // que des entreprises à administrateur unique, ce qui explique que l'élévation de privilège
    // corrigée par ces trois cas n'ait jamais été détectée).

    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.User)]
    public async Task Suppression_par_un_Viewer_ou_un_User_ne_supprime_que_son_propre_compte(UserRole role)
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, _, _, _) = await SeedCompanyDataAsync(client, adminAccessToken, adminUserId);
        var (otherUserId, _, otherAccessToken) = await AddUserToCompanyAsync(client, companyId, role);

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", otherAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Users.AnyAsync(u => u.Id == otherUserId));
        Assert.True(await context.Users.AnyAsync(u => u.Id == adminUserId));
        Assert.True(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.True(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
    }

    [Fact]
    public async Task Suppression_par_un_Admin_alors_qu_un_autre_Admin_existe_ne_supprime_que_son_propre_compte()
    {
        var client = fixture.CreateClient();
        var (companyId, adminAUserId, _, adminAAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, _, _, _) = await SeedCompanyDataAsync(client, adminAAccessToken, adminAUserId);
        var (adminBUserId, _, adminBAccessToken) = await AddUserToCompanyAsync(client, companyId, UserRole.Admin);

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", adminBAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Users.AnyAsync(u => u.Id == adminBUserId));
        Assert.True(await context.Users.AnyAsync(u => u.Id == adminAUserId));
        Assert.True(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.True(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
    }

    [Fact]
    public async Task Suppression_par_le_dernier_Admin_supprime_l_entreprise_et_tous_ses_comptes_meme_non_administrateurs()
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, _, _, _) = await SeedCompanyDataAsync(client, adminAccessToken, adminUserId);
        var (viewerUserId, _, _) = await AddUserToCompanyAsync(client, companyId, UserRole.Viewer);

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", adminAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Users.AnyAsync(u => u.Id == adminUserId));
        Assert.False(await context.Users.AnyAsync(u => u.Id == viewerUserId));
        Assert.False(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
    }

    // rse_indicators n'a longtemps porté aucune clé étrangère vers companies : la suppression de
    // l'entreprise laissait ses indicateurs orphelins. Les indicateurs d'une autre entreprise,
    // eux, doivent survivre : la cascade ne déborde pas de l'entreprise supprimée.
    [Fact]
    public async Task Suppression_par_le_dernier_Admin_supprime_les_indicateurs_de_l_entreprise_et_eux_seuls()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (otherCompanyId, _, _, _) = await RegisterCompanyAndLoginAdminAsync(client);

        Guid indicatorsId;
        Guid otherIndicatorsId;
        await using (var context = fixture.CreateDbContext())
        {
            var indicators = new RseIndicators(companyId, 2025);
            var otherIndicators = new RseIndicators(otherCompanyId, 2025);
            context.RseIndicators.AddRange(indicators, otherIndicators);
            await context.SaveChangesAsync();
            indicatorsId = indicators.Id;
            otherIndicatorsId = otherIndicators.Id;
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", adminAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verifyContext = fixture.CreateDbContext();
        Assert.False(await verifyContext.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await verifyContext.RseIndicators.AnyAsync(r => r.Id == indicatorsId));
        Assert.True(await verifyContext.RseIndicators.AnyAsync(r => r.Id == otherIndicatorsId));
    }

    // action_item_progress et support_tickets n'ont longtemps porté aucune clé étrangère : la
    // suppression de l'entreprise laissait orphelins les notes, responsables et descriptions de
    // tickets. Les tables déjà en cascade (historique, logo, déclarations, sites) sont vérifiées
    // au même endroit pour que la purge complète tienne dans un seul test.
    [Fact]
    public async Task Suppression_par_le_dernier_Admin_supprime_suivi_des_actions_tickets_logo_declarations_et_sites()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (otherCompanyId, otherUserId, _, otherAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, accessToken);
        var otherDiagnosticId = await CreateDiagnosticAsync(client, otherAccessToken);

        var (progressId, changeId, ticketId) = await SeedActionsTicketAndLogoAsync(companyId, userId, diagnosticId, [0x89, 0x50]);
        var (otherProgressId, otherChangeId, otherTicketId) =
            await SeedActionsTicketAndLogoAsync(otherCompanyId, otherUserId, otherDiagnosticId, [0x89, 0x50]);

        Guid statementId;
        Guid siteId;
        await using (var context = fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            var statement = new VsmeStatement(companyId, 2025, now);
            var site = new CompanySite(companyId, new CompanySiteDetails("Siège", "1 rue de Paris, 75001 Paris", SiteTenure.Owned, null, null), now);
            context.VsmeStatements.Add(statement);
            context.CompanySites.Add(site);
            await context.SaveChangesAsync();
            statementId = statement.Id;
            siteId = site.Id;
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verifyContext = fixture.CreateDbContext();
        Assert.False(await verifyContext.ActionItemProgresses.AnyAsync(p => p.Id == progressId));
        Assert.False(await verifyContext.ActionItemChanges.AnyAsync(c => c.Id == changeId));
        Assert.False(await verifyContext.SupportTickets.AnyAsync(t => t.Id == ticketId));
        Assert.False(await verifyContext.CompanyLogos.AnyAsync(l => l.CompanyId == companyId));
        Assert.False(await verifyContext.VsmeStatements.AnyAsync(s => s.Id == statementId));
        Assert.False(await verifyContext.CompanySites.AnyAsync(s => s.Id == siteId));

        Assert.True(await verifyContext.ActionItemProgresses.AnyAsync(p => p.Id == otherProgressId));
        Assert.True(await verifyContext.ActionItemChanges.AnyAsync(c => c.Id == otherChangeId));
        Assert.True(await verifyContext.SupportTickets.AnyAsync(t => t.Id == otherTicketId));
        Assert.True(await verifyContext.CompanyLogos.AnyAsync(l => l.CompanyId == otherCompanyId));
    }

    // Un compte qui n'est pas le dernier Admin emporte ses propres tickets (texte libre qu'il a
    // rédigé), jamais ceux des autres comptes. Le suivi des actions appartient à l'entreprise et
    // reste ; l'historique garde la ligne, sans auteur.
    [Fact]
    public async Task Suppression_par_un_compte_non_dernier_Admin_supprime_ses_tickets_et_eux_seuls()
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, adminAccessToken);
        var (adminProgressId, _, adminTicketId) = await SeedActionsTicketAndLogoAsync(companyId, adminUserId, diagnosticId, [0x89, 0x50]);
        var (viewerUserId, _, viewerAccessToken) = await AddUserToCompanyAsync(client, companyId, UserRole.Viewer);

        Guid viewerTicketId;
        Guid viewerChangeId;
        await using (var context = fixture.CreateDbContext())
        {
            var viewerTicket = new SupportTicket(
                companyId, viewerUserId, 43, "https://github.com/exemple/support/issues/43", "Question", "Où trouver le rapport ?", "question");
            var viewerChange = new ActionItemChange(
                diagnosticId, "ENV-REC-01", new ActionItemFieldChange(ActionItemField.AssignedTo, null, "Responsable QSE"), viewerUserId, DateTimeOffset.UtcNow);
            context.SupportTickets.Add(viewerTicket);
            context.ActionItemChanges.Add(viewerChange);
            await context.SaveChangesAsync();
            viewerTicketId = viewerTicket.Id;
            viewerChangeId = viewerChange.Id;
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", viewerAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verifyContext = fixture.CreateDbContext();
        Assert.False(await verifyContext.Users.AnyAsync(u => u.Id == viewerUserId));
        Assert.False(await verifyContext.SupportTickets.AnyAsync(t => t.Id == viewerTicketId));
        Assert.True(await verifyContext.SupportTickets.AnyAsync(t => t.Id == adminTicketId));
        Assert.True(await verifyContext.ActionItemProgresses.AnyAsync(p => p.Id == adminProgressId));
        var anonymizedChange = await verifyContext.ActionItemChanges.SingleAsync(c => c.Id == viewerChangeId);
        Assert.Null(anonymizedChange.ChangedByUserId);
    }

    // Défense en profondeur : isLastAdmin (GET /api/auth/me) n'est qu'un champ d'affichage —
    // DELETE /api/me ne l'accepte pas en entrée (PasswordConfirmationRequest ne porte qu'un
    // Password) et ne décide qu'à partir de l'état réel en base (rôle de l'appelant relu
    // depuis la table User, nombre d'administrateurs recompté), jamais d'une valeur fournie par
    // le client. Un champ isLastAdmin injecté dans le corps est silencieusement ignoré par le
    // model binder (propriété JSON inconnue, comportement par défaut d'ASP.NET Core) — vérifié
    // ici dans les deux sens, contre l'API elle-même, pas seulement contre l'écran.

    [Fact]
    public async Task Viewer_ne_peut_pas_declencher_la_suppression_de_l_entreprise_en_injectant_isLastAdmin_dans_le_corps()
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, _, _, _) = await SeedCompanyDataAsync(client, adminAccessToken, adminUserId);
        var (viewerUserId, _, viewerAccessToken) = await AddUserToCompanyAsync(client, companyId, UserRole.Viewer);

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", viewerAccessToken, new { password = ValidPassword, isLastAdmin = true }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Users.AnyAsync(u => u.Id == viewerUserId));
        Assert.True(await context.Users.AnyAsync(u => u.Id == adminUserId));
        Assert.True(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.True(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
    }

    [Fact]
    public async Task Dernier_Admin_ne_peut_pas_eviter_la_suppression_de_l_entreprise_en_injectant_isLastAdmin_false()
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, _, _, _) = await SeedCompanyDataAsync(client, adminAccessToken, adminUserId);

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", adminAccessToken, new { password = ValidPassword, isLastAdmin = false }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Users.AnyAsync(u => u.Id == adminUserId));
        Assert.False(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
    }

    // Report.generated_by_user_id est en ON DELETE RESTRICT (ReportConfiguration) : supprimer
    // un compte qui n'est pas le dernier Admin ne doit emporter que SES propres rapports, sans
    // quoi la suppression du User échouerait sur cette contrainte — jamais ceux d'un autre
    // compte, qui doivent survivre avec le diagnostic auquel ils sont rattachés.
    [Fact]
    public async Task Suppression_par_un_compte_non_dernier_Admin_ayant_genere_un_rapport_reussit_et_ne_supprime_que_ce_rapport()
    {
        var client = fixture.CreateClient();
        var (companyId, adminUserId, _, adminAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, _, adminReportId, _, _) = await SeedCompanyDataAsync(client, adminAccessToken, adminUserId);
        var (viewerUserId, _, viewerAccessToken) = await AddUserToCompanyAsync(client, companyId, UserRole.Viewer);

        Guid viewerReportId;
        await using (var context = fixture.CreateDbContext())
        {
            var viewerReport = new Report(diagnosticId, viewerUserId);
            context.Reports.Add(viewerReport);
            await context.SaveChangesAsync();
            viewerReportId = viewerReport.Id;
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", viewerAccessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var verifyContext = fixture.CreateDbContext();
        Assert.False(await verifyContext.Users.AnyAsync(u => u.Id == viewerUserId));
        Assert.False(await verifyContext.Reports.AnyAsync(r => r.Id == viewerReportId));
        Assert.True(await verifyContext.Reports.AnyAsync(r => r.Id == adminReportId));
        Assert.True(await verifyContext.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
        Assert.True(await verifyContext.Users.AnyAsync(u => u.Id == adminUserId));
    }

    [Fact]
    public async Task Cas18_Export_contient_l_integralite_des_donnees_du_compte()
    {
        var client = fixture.CreateClient();
        var (_, userId, email, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, responseId, reportId, recommendationId, recommendationCompletedAt) =
            await SeedCompanyDataAsync(client, accessToken, userId);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        Assert.False(body.GetProperty("user").TryGetProperty("passwordHash", out _));

        Assert.Equal("Entreprise Test", body.GetProperty("company").GetProperty("name").GetString());

        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("id").GetGuid() == diagnosticId);
        Assert.Contains(
            body.GetProperty("responses").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == responseId);
        Assert.Contains(
            body.GetProperty("domainScores").EnumerateArray(),
            ds => ds.GetProperty("diagnosticId").GetGuid() == diagnosticId);
        Assert.Contains(
            body.GetProperty("reports").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == reportId);

        // is_completed/completed_at sont saisis par l'utilisateur, pas dérivés : leur
        // absence de l'export le rendrait incomplet au regard des art. 15 et 20.
        var diagnosticRecommendation = Assert.Single(
            body.GetProperty("diagnosticRecommendations").EnumerateArray(),
            dr => dr.GetProperty("diagnosticId").GetGuid() == diagnosticId
                && dr.GetProperty("recommendationId").GetGuid() == recommendationId);
        Assert.True(diagnosticRecommendation.GetProperty("isCompleted").GetBoolean());

        // PostgreSQL timestamptz tronque à la microseconde (DateTimeOffset va jusqu'au
        // tick, 100 ns) : une égalité stricte échouerait sur le dernier chiffre après
        // l'aller-retour en base, sans rapport avec la valeur perdue en export.
        Assert.Equal(
            recommendationCompletedAt,
            diagnosticRecommendation.GetProperty("completedAt").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));
    }

    // Indicateurs chiffrés, déclarations de la norme volontaire et sites : saisis par
    // l'entreprise, absents de l'export jusqu'ici. Une ligne d'une autre entreprise est seedée
    // à côté de chacune pour vérifier que l'export ne déborde pas de l'entreprise du principal.
    [Fact]
    public async Task Cas18_Export_contient_les_indicateurs_declarations_et_sites_de_l_entreprise()
    {
        var client = fixture.CreateClient();
        var (companyId, _, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (otherCompanyId, _, _, _) = await RegisterCompanyAndLoginAdminAsync(client);

        Guid indicatorsId;
        Guid statementId;
        Guid siteId;
        Guid otherIndicatorsId;
        Guid otherStatementId;
        Guid otherSiteId;
        var now = DateTimeOffset.UtcNow;
        await using (var context = fixture.CreateDbContext())
        {
            var indicators = new RseIndicators(companyId, 2025);
            indicators.Update(new RseIndicatorValues { Scope1Tco2e = 12.5, Scope2LocationTco2e = 3.5, RevenueEur = 850_000 });

            var statement = new VsmeStatement(companyId, 2025, now);
            statement.Update(new VsmeStatementValues
            {
                LegalForm = "SAS",
                Certifications = [new VsmeCertification("ISO 14001", "AFNOR", new DateOnly(2024, 3, 1), null)],
            }, now);

            var site = new CompanySite(
                companyId,
                new CompanySiteDetails("Entrepôt de Rungis", "1 rue de la Tour, 94150 Rungis", SiteTenure.Leased, true, "Zone humide"),
                now);
            site.Locate(48.75, 2.35, "1 Rue de la Tour 94150 Rungis");

            var otherIndicators = new RseIndicators(otherCompanyId, 2025);
            var otherStatement = new VsmeStatement(otherCompanyId, 2025, now);
            var otherSite = new CompanySite(
                otherCompanyId,
                new CompanySiteDetails("Siège", "2 place du Marché, 69001 Lyon", SiteTenure.Owned, null, null),
                now);

            context.RseIndicators.AddRange(indicators, otherIndicators);
            context.VsmeStatements.AddRange(statement, otherStatement);
            context.CompanySites.AddRange(site, otherSite);
            await context.SaveChangesAsync();

            indicatorsId = indicators.Id;
            statementId = statement.Id;
            siteId = site.Id;
            otherIndicatorsId = otherIndicators.Id;
            otherStatementId = otherStatement.Id;
            otherSiteId = otherSite.Id;
        }

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("rseIndicators", out var exportedIndicators), "rseIndicators absent de l'export");
        var indicatorsRow = Assert.Single(exportedIndicators.EnumerateArray());
        Assert.Equal(indicatorsId, indicatorsRow.GetProperty("id").GetGuid());
        Assert.Equal(2025, indicatorsRow.GetProperty("year").GetInt32());
        Assert.Equal(12.5, indicatorsRow.GetProperty("scope1Tco2e").GetDouble());
        Assert.Equal(850_000, indicatorsRow.GetProperty("revenueEur").GetDouble());
        Assert.NotEqual(otherIndicatorsId, indicatorsRow.GetProperty("id").GetGuid());

        Assert.True(body.TryGetProperty("vsmeStatements", out var exportedStatements), "vsmeStatements absent de l'export");
        var statementRow = Assert.Single(exportedStatements.EnumerateArray());
        Assert.Equal(statementId, statementRow.GetProperty("id").GetGuid());
        Assert.Equal("SAS", statementRow.GetProperty("legalForm").GetString());
        var certification = Assert.Single(statementRow.GetProperty("certifications").EnumerateArray());
        Assert.Equal("ISO 14001", certification.GetProperty("name").GetString());
        Assert.NotEqual(otherStatementId, statementRow.GetProperty("id").GetGuid());

        Assert.True(body.TryGetProperty("companySites", out var exportedSites), "companySites absent de l'export");
        var siteRow = Assert.Single(exportedSites.EnumerateArray());
        Assert.Equal(siteId, siteRow.GetProperty("id").GetGuid());
        Assert.Equal("Entrepôt de Rungis", siteRow.GetProperty("name").GetString());
        Assert.Equal("Leased", siteRow.GetProperty("tenure").GetString());
        Assert.Equal(48.75, siteRow.GetProperty("latitude").GetDouble());
        Assert.Equal("Zone humide", siteRow.GetProperty("sensitiveAreaName").GetString());
        Assert.NotEqual(otherSiteId, siteRow.GetProperty("id").GetGuid());
    }

    // Suivi du plan d'actions, historique, tickets de support et logo : saisis par l'entreprise,
    // absents de l'export jusqu'ici. Le logo est attendu en base64 dans le JSON, pour qu'un
    // export reste un fichier unique et complet.
    [Fact]
    public async Task Cas18_Export_contient_le_suivi_des_actions_les_tickets_et_le_logo()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (otherCompanyId, otherUserId, _, otherAccessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var diagnosticId = await CreateDiagnosticAsync(client, accessToken);
        var otherDiagnosticId = await CreateDiagnosticAsync(client, otherAccessToken);

        byte[] logo = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];
        var (progressId, changeId, ticketId) = await SeedActionsTicketAndLogoAsync(companyId, userId, diagnosticId, logo);
        await SeedActionsTicketAndLogoAsync(otherCompanyId, otherUserId, otherDiagnosticId, [0x89, 0x50, 0x4E, 0x47]);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("actionItemProgress", out var exportedProgress), "actionItemProgress absent de l'export");
        var progressRow = Assert.Single(exportedProgress.EnumerateArray());
        Assert.Equal(progressId, progressRow.GetProperty("id").GetGuid());
        Assert.Equal(diagnosticId, progressRow.GetProperty("diagnosticId").GetGuid());
        Assert.Equal("InProgress", progressRow.GetProperty("status").GetString());
        Assert.Equal("Responsable QSE", progressRow.GetProperty("assignedTo").GetString());
        Assert.Equal("Devis demandé.", progressRow.GetProperty("notes").GetString());

        Assert.True(body.TryGetProperty("actionItemChanges", out var exportedChanges), "actionItemChanges absent de l'export");
        var changeRow = Assert.Single(exportedChanges.EnumerateArray());
        Assert.Equal(changeId, changeRow.GetProperty("id").GetGuid());
        Assert.Equal("InProgress", changeRow.GetProperty("newValue").GetString());

        Assert.True(body.TryGetProperty("supportTickets", out var exportedTickets), "supportTickets absent de l'export");
        var ticketRow = Assert.Single(exportedTickets.EnumerateArray());
        Assert.Equal(ticketId, ticketRow.GetProperty("id").GetGuid());
        Assert.Equal("Export PDF vide", ticketRow.GetProperty("title").GetString());
        Assert.Equal("Le rapport s'ouvre sans page de garde.", ticketRow.GetProperty("description").GetString());

        Assert.True(body.TryGetProperty("companyLogo", out var exportedLogo), "companyLogo absent de l'export");
        Assert.Equal("image/png", exportedLogo.GetProperty("contentType").GetString());
        Assert.Equal(logo, exportedLogo.GetProperty("pngContent").GetBytesFromBase64());
    }

    [Fact]
    public async Task Export_avec_mot_de_passe_de_confirmation_errone_repond_401()
    {
        var client = fixture.CreateClient();
        var (_, _, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Post, "/api/me/export", accessToken, new { password = "MotDePasseErrone2026!" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cas19_Suppression_de_compte_ne_laisse_aucune_ligne_residuelle_dans_les_neuf_tables()
    {
        var client = fixture.CreateClient();
        var (companyId, userId, email, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (diagnosticId, responseId, reportId, _, _) = await SeedCompanyDataAsync(client, accessToken, userId);

        // Capture des jetons émis pendant l'inscription/connexion, avant suppression.
        await using (var preCheckContext = fixture.CreateDbContext())
        {
            Assert.True(await preCheckContext.RefreshTokens.AnyAsync(rt => rt.UserId == userId));
            Assert.True(await preCheckContext.EmailVerificationTokens.AnyAsync(t => t.UserId == userId));
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();

        Assert.False(await context.Users.AnyAsync(u => u.Id == userId));
        Assert.False(await context.RefreshTokens.AnyAsync(rt => rt.UserId == userId));
        Assert.False(await context.EmailVerificationTokens.AnyAsync(t => t.UserId == userId));
        Assert.False(await context.Companies.AnyAsync(c => c.Id == companyId));
        Assert.False(await context.Diagnostics.AnyAsync(d => d.Id == diagnosticId));
        Assert.False(await context.Responses.AnyAsync(r => r.Id == responseId));
        Assert.False(await context.DomainScores.AnyAsync(ds => ds.DiagnosticId == diagnosticId));
        Assert.False(await context.DiagnosticRecommendations.AnyAsync(dr => dr.DiagnosticId == diagnosticId));
        Assert.False(await context.Reports.AnyAsync(r => r.Id == reportId));

        _ = email;
    }

    [Fact]
    public async Task Suppression_avec_mot_de_passe_de_confirmation_errone_repond_401_et_ne_supprime_rien()
    {
        var client = fixture.CreateClient();
        var (_, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);

        var response = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = "MotDePasseErrone2026!" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var context = fixture.CreateDbContext();
        Assert.True(await context.Users.AnyAsync(u => u.Id == userId));
    }

    [Fact]
    public async Task Cas20_Suppression_de_compte_laisse_les_tables_de_reference_intactes()
    {
        var client = fixture.CreateClient();
        var (_, userId, _, accessToken) = await RegisterCompanyAndLoginAdminAsync(client);
        var (_, _, _, recommendationId, _) = await SeedCompanyDataAsync(client, accessToken, userId);

        int questionCountBefore;
        await using (var beforeContext = fixture.CreateDbContext())
        {
            questionCountBefore = await beforeContext.Questions.CountAsync();
        }

        var deleteResponse = await client.SendAsync(
            AuthorizedRequest(HttpMethod.Delete, "/api/me", accessToken, new { password = ValidPassword }));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var context = fixture.CreateDbContext();

        // Table de référence : la Recommendation elle-même survit, seule la ligne de
        // jointure DiagnosticRecommendation (déjà vérifiée absente au cas 19) disparaît.
        Assert.True(await context.Recommendations.AnyAsync(r => r.Id == recommendationId));
        Assert.Equal(questionCountBefore, await context.Questions.CountAsync());
        Assert.True(await context.SectorWeights.AnyAsync());
    }
}
