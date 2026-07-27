using System.Security.Cryptography;

namespace MAAT.Application.Security;

public static class SecureTokenGenerator
{
    public static string Generate(int byteLength = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
