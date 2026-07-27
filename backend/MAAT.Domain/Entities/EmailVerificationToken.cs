namespace MAAT.Domain.Entities;

public class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private EmailVerificationToken()
    {
    }

    public EmailVerificationToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
