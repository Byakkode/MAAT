using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using MAAT.Infrastructure.Email;
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
        };
    });

builder.Services.AddAuthorization();

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
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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

app.UseHttpsRedirection();

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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
