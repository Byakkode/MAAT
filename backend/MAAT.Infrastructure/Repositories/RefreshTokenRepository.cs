using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using MAAT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class RefreshTokenRepository(MaatDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken> IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var token = new RefreshToken(userId, TokenHasher.Sha256Hex(plaintextToken), expiresAt);
        await context.RefreshTokens.AddAsync(token, ct);
        return token;
    }

    public Task<RefreshToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct)
    {
        var hash = TokenHasher.Sha256Hex(plaintextToken);
        return context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
    }

    public Task RevokeAsync(RefreshToken token, CancellationToken ct)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    public async Task RevokeAllActiveForUserExceptAsync(Guid userId, Guid exceptTokenId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.Id != exceptTokenId)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    public Task<int> PurgeExpiredOrRevokedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        context.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoff || (rt.RevokedAt != null && rt.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);
}
