using System.Security.Cryptography;
using System.Text;

namespace MAAT.Infrastructure.Security;

// SHA-256 plutôt que bcrypt : les refresh/verification tokens ont déjà 256 bits d'entropie
// (générés aléatoirement), contrairement à un mot de passe. Un hachage lent n'apporte
// aucune protection supplémentaire ici et empêcherait la recherche indexée en base.
// Voir docs/adr/0004-hachage-des-jetons-et-liste-locale-de-mots-de-passe.md
public static class TokenHasher
{
    public static string Sha256Hex(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
