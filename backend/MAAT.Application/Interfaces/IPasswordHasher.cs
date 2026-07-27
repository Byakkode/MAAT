namespace MAAT.Application.Interfaces;

public interface IPasswordHasher
{
    // Hash fixe précalculé, vérifié même quand l'utilisateur n'existe pas pour éviter une fuite par timing.
    string DummyHash { get; }

    string Hash(string password);

    bool Verify(string password, string hash);
}
