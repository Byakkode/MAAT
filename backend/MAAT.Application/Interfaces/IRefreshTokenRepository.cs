using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> IssueAsync(Guid userId, string plaintextToken, DateTimeOffset expiresAt, CancellationToken ct);

    Task<RefreshToken?> FindByPlaintextAsync(string plaintextToken, CancellationToken ct);

    Task RevokeAsync(RefreshToken token, CancellationToken ct);

    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct);

    // Changement de mot de passe (auth-securite-rgpd.md à venir, section "Mot de passe" de
    // coquille-et-compte.md) : "invalide les autres sessions" — la session qui vient de saisir
    // l'ancien mot de passe avec succès reste connectée, contrairement à la détection de
    // réutilisation (RevokeAllActiveForUserAsync ci-dessus), qui n'a par construction aucune
    // session légitime à préserver.
    Task RevokeAllActiveForUserExceptAsync(Guid userId, Guid exceptTokenId, CancellationToken ct);

    Task<int> PurgeExpiredOrRevokedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}
