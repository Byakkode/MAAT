using MAAT.Application.Interfaces;

namespace MAAT.Infrastructure.Security;

public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    private static readonly Lazy<string> LazyDummyHash = new(() =>
        BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing-safety", WorkFactor));

    public string DummyHash => LazyDummyHash.Value;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
