using MAAT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAAT.Infrastructure.Email;

// Stand-in en attendant l'intégration d'un fournisseur transactionnel européen
// (Brevo, Scaleway TEM — cf. CLAUDE.md). N'envoie aucun e-mail réel.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct)
    {
        logger.LogInformation("Email de vérification (non envoyé, fournisseur non branché) pour {Email}", toEmail);
        return Task.CompletedTask;
    }

    public Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct)
    {
        logger.LogInformation("Notification de tentative d'inscription en double (non envoyée, fournisseur non branché) pour {Email}", toEmail);
        return Task.CompletedTask;
    }
}
