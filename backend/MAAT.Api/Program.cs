using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MAAT.Api.Security;
using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using MAAT.Domain.Services;
using MAAT.Infrastructure.Email;
using MAAT.Infrastructure.Jobs;
using MAAT.Infrastructure.Pdf;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Repositories;
using MAAT.Infrastructure.Security;
using MAAT.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<MaatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ICompromisedPasswordChecker, LocalListCompromisedPasswordChecker>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Section 4 de la spec : cloisonnement par entreprise. ICurrentUserContext résout
// company_id depuis les claims du principal authentifié (jamais depuis une entrée
// client) ; les dépôts ci-dessous filtrent par cette valeur à la construction de
// chaque requête, pas après récupération.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddScoped<IDiagnosticRepository, DiagnosticRepository>();
builder.Services.AddScoped<IResponseRepository, ResponseRepository>();
builder.Services.AddScoped<IDomainScoreRepository, DomainScoreRepository>();
builder.Services.AddScoped<IDiagnosticRecommendationRepository, DiagnosticRecommendationRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

// docs/specs/dashboard.md, section 5 : seule exception au cloisonnement par entreprise
// ci-dessus, voir son commentaire dans ISectorBenchmarkRepository.
builder.Services.AddScoped<ISectorBenchmarkRepository, SectorBenchmarkRepository>();

// Tables de référence (docs/specs/modele-donnees.md) : ni company-scopées, ni dépendantes
// d'ICurrentUserContext, contrairement aux dépôts ci-dessus.
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<ISectorWeightRepository, SectorWeightRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();

// Charge Question, Recommendation et SectorWeight depuis MAAT.Infrastructure/Seed/*.csv
// (docs/specs/modele-donnees.md) — jamais depuis les migrations, qui ne portent que le
// schéma. Invoqué plus bas, jamais consommé directement par ce fichier au-delà de cet
// enregistrement (docs/adr/0005).
builder.Services.AddScoped<ReferenceDataSeeder>();

// Sans état, donc Singleton : injecté (plutôt qu'instancié directement) pour que le cas 15
// de questionnaire.md puisse substituer une implémentation qui lève, en test.
builder.Services.AddSingleton<IScoringService, ScoringService>();

// Même principe que IScoringService ci-dessus : substitué en test pour le cas 12 de
// docs/specs/recommandations.md (échec de la sélection → complétion annulée).
builder.Services.AddSingleton<IRecommendationEngine, RecommendationEngine>();

// docs/specs/rapport-pdf.md, section 3 : aucun DateTime.Now dans le générateur de rapport —
// l'horloge est injectée, ce qui rend le déterminisme testable en la figeant (cas 10 à 12).
builder.Services.AddSingleton(TimeProvider.System);

// Substitué en test pour le cas 8 (échec de la génération → aucune ligne Report), même
// principe que IScoringService/IRecommendationEngine ci-dessus.
builder.Services.AddSingleton<IReportGenerator, QuestPdfReportGenerator>();

// Jeu de données de démonstration (commande "seed", drapeau demo, Development uniquement) :
// voir DemoDataSeeder. Jamais invoqué au démarrage, contrairement à ReferenceDataSeeder
// ci-dessus.
builder.Services.AddScoped<DemoDataSeeder>();

builder.Services.AddScoped<DiagnosticService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccountService>();

// LoggingEmailSender journalise les adresses e-mail (donnée personnelle), ce que
// la section 5 de la spec interdit en dehors du poste de développement. Aucun
// fournisseur réel n'est encore branché (Brevo, Scaleway TEM — cf. CLAUDE.md et
// ADR 0004) ; le garde-fou juste après builder.Build() fait échouer le démarrage
// si aucun IEmailSender n'est enregistré hors Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}

builder.Services.AddScoped<AuthService>();
builder.Services.AddHostedService<RefreshTokenPurgeService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Lu au moment de la résolution des options (après builder.Build()), pas à
        // l'enregistrement du service : en test (WebApplicationFactory), la
        // configuration injectée par WithWebHostBuilder n'est fusionnée qu'à ce
        // moment-là.
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var signingKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException("Configuration manquante : Jwt:SigningKey (32 octets minimum).");

        // Conserve les noms de claims exacts (sub, company_id, role) au lieu du
        // remappage par défaut vers les URI ClaimTypes de .NET.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "maat-api",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "maat-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            // Le claim "role" (pas le ClaimTypes.Role par défaut) porte le rôle métier :
            // sans ce mapping, [Authorize(Roles = "...")] ne le verrait jamais (section 4).
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization();

// Section 5 de la spec : liste blanche explicite, jamais de joker "*" — incompatible
// avec AllowCredentials de toute façon, nécessaire au cookie de refresh cross-site.
// Lue via IConfiguration injecté par Configure<T>, pas via builder.Configuration ici :
// même piège que pour Jwt:SigningKey plus bas — en test (WebApplicationFactory), la
// configuration injectée par WithWebHostBuilder n'est fusionnée qu'après builder.Build(),
// donc un accès à builder.Configuration à ce point du script ne la verrait jamais.
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            // docs/specs/rapport-pdf.md, section 2 : Content-Disposition ne fait pas partie de
            // la liste des en-têtes exposés par défaut aux réponses cross-origin (contrairement
            // aux en-têtes de requête, couverts par AllowAnyHeader ci-dessus) — sans ceci,
            // response.headers.get("Content-Disposition") renvoie toujours null côté navigateur
            // et le nom de fichier déterministe du rapport ne serait jamais lisible par le
            // frontend qui déclenche le téléchargement (section 6).
            .WithExposedHeaders("Content-Disposition"));
});

// includeSubDomains explicite : la valeur par défaut de HstsOptions est false.
builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
    {
        var email = context.Items.TryGetValue("RateLimitEmail", out var value) ? value as string : null;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"{ip}:{email ?? "unknown"}";

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            });
    });

    // POST /api/me/export et DELETE /api/me acceptent un mot de passe de confirmation
    // (section 6) : sans limitation, ils forment un oracle de mot de passe hors du
    // chemin /api/auth/login, sans les protections de la section 2. Partitionné par
    // utilisateur authentifié (pas par IP+email : l'identité est déjà connue via le
    // JWT) — les deux endpoints partagent la même politique nommée, donc le même
    // compteur par utilisateur : alterner entre eux n'augmente pas le quota.
    //
    // La clé est lue depuis context.Items (renseigné par un middleware juste après
    // UseAuthentication, cf. plus bas), pas directement depuis context.User ici : même
    // principe que la politique "login" ci-dessus, qui lit context.Items plutôt que de
    // reparser la requête à cet endroit.
    options.AddPolicy("password-confirmation", context =>
    {
        var userId = context.Items.TryGetValue("RateLimitUserId", out var value) ? value as string : null;

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            userId ?? "anonymous",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            });
    });

    // docs/specs/rapport-pdf.md, section 2 : la génération est l'opération la plus coûteuse
    // en CPU du produit (mise en page, rendu d'une image, embarquement de polices). Même
    // mécanisme et même clé de partition (context.Items["RateLimitUserId"], voir la politique
    // "password-confirmation" ci-dessus et le middleware qui la renseigne plus bas) que les
    // endpoints de confirmation de mot de passe, quota nettement plus large — ordre de
    // grandeur d'une dizaine de générations par quart d'heure, à ajuster après mesure réelle.
    options.AddPolicy("report-generation", context =>
    {
        var userId = context.Items.TryGetValue("RateLimitUserId", out var value) ? value as string : null;

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            userId ?? "anonymous",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            });
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// "seed --validate" relit les CSV et ne touche ni base de données ni JWT : traité en tout
// premier, avant les gardes ci-dessous (clé JWT, IEmailSender), qui exigent une
// configuration que cette commande n'a pas à fournir (docs/specs/modele-donnees.md). Elle
// doit fonctionner avec DOTNET_ENVIRONMENT=Production et aucune configuration.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase) && args.Contains("--validate", StringComparer.OrdinalIgnoreCase))
{
    var result = ReferenceDataValidator.Validate();

    foreach (var warning in result.Warnings)
    {
        app.Logger.LogWarning("{Warning}", warning);
    }

    foreach (var error in result.Errors)
    {
        app.Logger.LogError("{Error}", error);
    }

    if (result.IsValid)
    {
        app.Logger.LogInformation(
            "Fichiers de seed valides ({WarningCount} avertissement(s) de calibrage).", result.Warnings.Count);
        return;
    }

    app.Logger.LogError("Fichiers de seed invalides : {ErrorCount} erreur(s).", result.Errors.Count);
    Environment.Exit(1);
}

// Force la construction du singleton ICompromisedPasswordChecker (et donc le
// chargement de la liste de mots de passe compromis en HashSet, cf.
// LocalListCompromisedPasswordChecker) au démarrage plutôt qu'à la première
// inscription venue — docs/specs/auth-securite-rgpd.md, section 1.
using (var passwordCheckerWarmupScope = app.Services.CreateScope())
{
    passwordCheckerWarmupScope.ServiceProvider.GetRequiredService<ICompromisedPasswordChecker>();
}

// docs/adr/0006-licence-questpdf.md : sans cet appel explicite, QuestPDF lève une exception
// au premier document généré (échec de runtime, pas de compilation) — appelé ici pour que ce
// soit le démarrage qui échoue bruyamment, pas le premier téléchargement réel. Enregistre
// aussi les polices Poppins/Inter embarquées (docs/specs/rapport-pdf.md, section 5).
QuestPdfBootstrapper.Configure();

// Vérifié au démarrage plutôt qu'à la résolution paresseuse des JwtBearerOptions
// (cf. commentaire plus haut) : une clé trop courte affaiblit HMAC-SHA256 et doit
// bloquer le démarrage dans tous les environnements (docs/specs/auth-securite-rgpd.md,
// section 5), pas seulement en production.
var jwtSigningKey = app.Configuration["Jwt:SigningKey"];
if (string.IsNullOrEmpty(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException(
        "Configuration invalide : Jwt:SigningKey doit faire au moins 32 octets une fois encodée " +
        "en UTF-8 (docs/specs/auth-securite-rgpd.md, section 5).");
}

if (!app.Environment.IsDevelopment())
{
    using var startupScope = app.Services.CreateScope();
    if (startupScope.ServiceProvider.GetService<IEmailSender>() is null)
    {
        throw new InvalidOperationException(
            "Aucun IEmailSender réel n'est configuré. LoggingEmailSender journalise les adresses " +
            "e-mail et est réservé à l'environnement Development (docs/specs/auth-securite-rgpd.md, " +
            "section 5). Configurez un fournisseur transactionnel européen (Brevo, Scaleway TEM) " +
            "avant de démarrer l'application hors Development.");
    }
}

// Données de référence (Question, Recommendation, SectorWeight) : chargées depuis
// MAAT.Infrastructure/Seed/*.csv (docs/specs/modele-donnees.md), jamais depuis les
// migrations. Automatique en Development à chaque démarrage (idempotent, cf.
// ReferenceDataSeeder) ; commande explicite "seed" dans les autres environnements — ex.
// `dotnet MAAT.Api.dll seed` — pour ne pas semer à l'insu d'un déploiement en production.
async Task RunReferenceDataSeedAsync()
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
    if (await seeder.SeedAsync())
    {
        app.Logger.LogInformation("Données de référence (questions, recommandations, pondérations sectorielles) chargées.");
    }
    else
    {
        app.Logger.LogWarning(
            "Données de référence non chargées : migrations en attente. Exécutez "
                + "'dotnet ef database update --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api' "
                + "avant de relancer.");
    }
}

// Jeu de données de démonstration (docs/specs/modele-donnees.md) : "seed" avec le drapeau
// demo, jamais au démarrage, jamais confondu avec RunReferenceDataSeedAsync ci-dessus.
// DemoDataSeeder revérifie lui-même IHostEnvironment.IsDevelopment (défense en profondeur) ;
// le refus explicite ci-dessous évite en plus une connexion à la base pour rien hors
// Development.
async Task RunDemoDataSeedAsync()
{
    using var demoScope = app.Services.CreateScope();
    var demoSeeder = demoScope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    if (await demoSeeder.SeedAsync())
    {
        app.Logger.LogInformation("Jeu de données de démonstration chargé (45 questions, 2 secteurs NAF, 3 entreprises fictives).");
    }
    else
    {
        app.Logger.LogWarning(
            "Jeu de données de démonstration non chargé : migrations en attente. Exécutez "
                + "'dotnet ef database update --project backend/MAAT.Infrastructure --startup-project backend/MAAT.Api' "
                + "avant de relancer.");
    }
}

if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
    {
        if (!app.Environment.IsDevelopment())
        {
            app.Logger.LogError("seed --demo est réservé à l'environnement Development : jamais en production.");
            Environment.Exit(1);
        }

        await RunDemoDataSeedAsync();
        return;
    }

    // Commande explicite : une base de données inaccessible doit faire échouer le
    // processus bruyamment, pas être avalée.
    await RunReferenceDataSeedAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    // Confort de développement, jamais bloquant : une base pas encore démarrée ou une
    // chaîne de connexion inerte (ex. JwtSigningKeyStartupTests, qui ne visent pas du
    // tout la base) ne doit pas empêcher l'application de démarrer.
    try
    {
        await RunReferenceDataSeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Chargement des données de référence ignoré au démarrage (base de données inaccessible ?).");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Comme le reste de l'écosystème ASP.NET Core, HSTS n'a d'effet qu'en dehors de
// Development : il indique au navigateur de forcer HTTPS pour ce domaine pendant
// options.MaxAge, ce qui gênerait le développement local en HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// En-têtes de durcissement (section 5) posés sur toute réponse, y compris les 404 de
// routage : X-Content-Type-Options et Referrer-Policy n'ont pas d'équivalent middleware
// intégré dans ASP.NET Core, contrairement à HSTS et CORS.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'none'");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseCors("Default");

// Capture l'email du corps de requête pour partitionner la limitation de débit
// du login par couple IP + email, sans consommer le flux avant le model binding.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
        && HttpMethods.IsPost(context.Request.Method))
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                context.Items["RateLimitEmail"] = emailProp.GetString()?.Trim().ToLowerInvariant();
            }
        }
        catch (JsonException)
        {
            // Corps invalide : laissé à la validation du modèle en aval.
        }
    }

    await next();
});

// Authentication/Authorization avant RateLimiter : la politique "password-confirmation"
// partitionne par utilisateur authentifié (claim "sub"), qui n'existe sur context.User
// qu'une fois UseAuthentication passé. Ça ne change rien pour la politique "login"
// (endpoint anonyme, UseAuthorization n'y fait rien) ni pour les endpoints protégés
// (un principal non authentifié y est de toute façon rejeté par UseAuthorization avant
// même d'atteindre le rate limiter — cohérent, puisque cette limitation vise à borner
// les tentatives d'un utilisateur authentifié, pas à protéger la porte [Authorize]
// elle-même).
app.UseAuthentication();
app.UseAuthorization();

// Capture le claim "sub" dans context.Items pour la politique "password-confirmation" :
// le lire directement depuis context.User à l'intérieur du partitioner (plutôt que de le
// capturer ici, une seule fois par requête) s'est avéré source d'un comptage instable
// sous charge séquentielle rapide en test — même précaution que pour RateLimitEmail
// ci-dessus, appliquée ici pour la même raison de robustesse, pas pour éviter de
// consommer le corps de la requête.
app.Use(async (context, next) =>
{
    context.Items["RateLimitUserId"] = context.User.FindFirst("sub")?.Value;
    await next();
});

app.UseRateLimiter();

app.MapControllers();

await app.RunAsync();

public partial class Program { }
