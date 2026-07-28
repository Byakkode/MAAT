using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MAAT.Api.Security;
using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using MAAT.Domain.Services;
using MAAT.Infrastructure.Email;
using MAAT.Infrastructure.Jobs;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Repositories;
using MAAT.Infrastructure.Security;
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

// Tables de référence (docs/specs/modele-donnees.md) : ni company-scopées, ni dépendantes
// d'ICurrentUserContext, contrairement aux dépôts ci-dessus.
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<ISectorWeightRepository, SectorWeightRepository>();

// Sans état, donc Singleton : injecté (plutôt qu'instancié directement) pour que le cas 15
// de questionnaire.md puisse substituer une implémentation qui lève, en test.
builder.Services.AddSingleton<IScoringService, ScoringService>();

builder.Services.AddScoped<DiagnosticService>();
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
            .AllowCredentials());
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
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Force la construction du singleton ICompromisedPasswordChecker (et donc le
// chargement de la liste de mots de passe compromis en HashSet, cf.
// LocalListCompromisedPasswordChecker) au démarrage plutôt qu'à la première
// inscription venue — docs/specs/auth-securite-rgpd.md, section 1.
using (var passwordCheckerWarmupScope = app.Services.CreateScope())
{
    passwordCheckerWarmupScope.ServiceProvider.GetRequiredService<ICompromisedPasswordChecker>();
}

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

app.Run();

public partial class Program { }
