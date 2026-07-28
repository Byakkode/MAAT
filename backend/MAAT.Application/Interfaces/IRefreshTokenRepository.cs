using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct);

    Task<RefreshToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct);

    Task RevokeAsync(RefreshToken token, CancellationToken ct);

    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct);

    Task<int> PurgeExpiredOrRevokedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}
