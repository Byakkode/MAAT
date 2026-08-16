using MAAT.Infrastructure.Email;
using Microsoft.Extensions.Logging;

namespace MAAT.IntegrationTests;

// NullEmailSender (ADR 0009, Email__Provider=none) ne doit jamais journaliser
// d'adresse e-mail ni de jeton — seulement le fait qu'un envoi a été demandé et son
// type (docs/specs/auth-securite-rgpd.md, section 5). Ces tests inspectent
// directement les messages formatés, pas juste le comportement "aucune exception".
public class NullEmailSenderTests
{
    private const string Email = "jean.dupont@exemple.fr";
    private const string Token = "un-jeton-secret-de-verification";

    [Fact]
    public async Task SendVerificationEmailAsync_ne_journalise_ni_adresse_ni_jeton()
    {
        var logger = new CapturingLogger<NullEmailSender>();
        var sender = new NullEmailSender(logger);

        await sender.SendVerificationEmailAsync(Email, Token, CancellationToken.None);

        var logged = string.Join(" | ", logger.Messages);
        Assert.DoesNotContain(Email, logged, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendDuplicateRegistrationAttemptNoticeAsync_ne_journalise_pas_l_adresse()
    {
        var logger = new CapturingLogger<NullEmailSender>();
        var sender = new NullEmailSender(logger);

        await sender.SendDuplicateRegistrationAttemptNoticeAsync(Email, CancellationToken.None);

        Assert.DoesNotContain(Email, string.Join(" | ", logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Journalise_neanmoins_le_type_de_message_dont_l_envoi_a_ete_demande()
    {
        var logger = new CapturingLogger<NullEmailSender>();
        var sender = new NullEmailSender(logger);

        await sender.SendVerificationEmailAsync(Email, Token, CancellationToken.None);
        await sender.SendDuplicateRegistrationAttemptNoticeAsync(Email, CancellationToken.None);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("vérification", logger.Messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("double", logger.Messages[1], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
