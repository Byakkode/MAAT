using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct);

    Task<EmailVerificationToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct);

    Task MarkConsumedAsync(EmailVerificationToken token, CancellationToken ct);
}
