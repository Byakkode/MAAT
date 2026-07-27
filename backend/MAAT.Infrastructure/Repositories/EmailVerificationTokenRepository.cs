using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class EmailVerificationTokenRepository(MaatDbContext context) : IEmailVerificationTokenRepository
{
    public async Task IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var token = new EmailVerificationToken(userId, TokenHasher.Sha256Hex(plaintextToken), expiresAt);
        await context.EmailVerificationTokens.AddAsync(token, ct);
    }

    public Task<EmailVerificationToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct)
    {
        var hash = TokenHasher.Sha256Hex(plaintextToken);
        return context.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    public Task MarkConsumedAsync(EmailVerificationToken token, CancellationToken ct)
    {
        token.ConsumedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
