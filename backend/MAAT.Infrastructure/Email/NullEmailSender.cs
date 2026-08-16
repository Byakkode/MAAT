using MAAT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAAT.Infrastructure.Email;

// Activé explicitement par Email__Provider=none, jamais par défaut (cf. Program.cs,
// ADR 0009). Coupe-circuit assumé pour démarrer en Production sans fournisseur
// transactionnel encore provisionné : n'envoie rien et ne journalise ni adresse e-mail
// ni jeton (donnée personnelle / secret — docs/specs/auth-securite-rgpd.md, section 5),
// seulement le type de message dont l'envoi a été demandé.
public class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct)
    {
        logger.LogInformation(
            "Email non envoyé (Email__Provider=none) : demande de vérification d'adresse.");
        return Task.CompletedTask;
    }

    public Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct)
    {
        logger.LogInformation(
            "Email non envoyé (Email__Provider=none) : notification de tentative d'inscription en double.");
        return Task.CompletedTask;
    }
}
