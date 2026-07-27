using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public Guid CompanyId { get; set; }
    public UserRole Role { get; set; }
    public bool EmailVerified { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User()
    {
    }

    public User(string email, string passwordHash, Guid companyId, UserRole role = UserRole.User)
    {
        Id = Guid.NewGuid();
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        CompanyId = companyId;
        Role = role;
        EmailVerified = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
