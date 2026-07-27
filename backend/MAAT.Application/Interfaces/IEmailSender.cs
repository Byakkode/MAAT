namespace MAAT.Application.Interfaces;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string toEmail, string verificationToken, CancellationToken ct);

    Task SendDuplicateRegistrationAttemptNoticeAsync(string toEmail, CancellationToken ct);
}
