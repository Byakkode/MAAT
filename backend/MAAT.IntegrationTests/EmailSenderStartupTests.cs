using MAAT.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MAAT.IntegrationTests;

// LoggingEmailSender journalise les adresses e-mail (donnée personnelle), interdit
// par la section 5 de la spec en dehors du développement. Ces tests vérifient que
// Program.cs ne l'enregistre qu'en Development et refuse de démarrer ailleurs si
// aucun IEmailSender réel n'a été configuré à la place.
public class EmailSenderStartupTests
{
    private static Dictionary<string, string?> ProductionConfig() => new()
    {
        ["ConnectionStrings:Default"] = "Host=localhost;Port=5433;Database=maat;Username=maat;Password=unused",
        ["Jwt:SigningKey"] = "production-test-signing-key-32-bytes-minimum-xyz",
    };

    [Fact]
    public void Demarrage_hors_Development_sans_email_sender_reel_echoue()
    {
        Exception? startupFailure = null;

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(ProductionConfig()));
            });

            _ = factory.Services;
        }
        catch (Exception ex)
        {
            startupFailure = ex;
        }

        Assert.NotNull(startupFailure);
        Assert.Contains("IEmailSender", FlattenMessages(startupFailure!), StringComparison.Ordinal);
    }

    [Fact]
    public void Demarrage_hors_Development_avec_un_email_sender_reel_reussit()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(ProductionConfig()));
            builder.ConfigureServices(services => services.AddScoped<IEmailSender, NoOpEmailSender>());
        });

        Assert.NotNull(factory.Services);

        using var scope = factory.Services.CreateScope();
        Assert.IsType<NoOpEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        var current = exception;
        while (current is not null)
        {
            messages.Add(current.Message);
            current = current.InnerException;
        }

        return string.Join(" | ", messages);
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
