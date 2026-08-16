using System.Collections.Concurrent;
using MAAT.Application.Interfaces;

namespace MAAT.IntegrationTests.TestDoubles;

// Les tests de renvoi de vérification (ResendVerificationTests) ont besoin du jeton en clair
// envoyé par e-mail pour vérifier qu'un ancien jeton devient invalide après un renvoi — la
// base ne stocke que son hash (TokenHasher), donc LoggingEmailSender (qui se contente de
// journaliser) ne suffit pas. Enregistré comme instance singleton unique via
// ServiceCollection.Replace dans le fixture de test, pour que les appels HTTP passant par la
// même instance en mémoire que celle lue ensuite par les assertions.
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string Email, string Token)> _verificationEmails = new();

    public Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct)
    {
        _verificationEmails.Enqueue((toEmail, verificationToken));
        return Task.CompletedTask;
    }

    public Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct) => Task.CompletedTask;

    // Ordre d'émission, du plus ancien au plus récent : permet de distinguer le jeton
    // d'inscription du jeton de renvoi pour une même adresse.
    public IReadOnlyList<string> TokensFor(string email) =>
        _verificationEmails.Where(e => e.Email == email).Select(e => e.Token).ToList();
}
