using System.Reflection;
using MAAT.Application.Interfaces;

namespace MAAT.Infrastructure.Security;

// Repli local documenté en ADR : évite tout appel réseau (même en k-anonymat HIBP)
// depuis le serveur d'inscription. Voir docs/adr/0004-hachage-des-jetons-et-liste-locale-de-mots-de-passe.md
public class LocalListCompromisedPasswordChecker : ICompromisedPasswordChecker
{
    private static readonly Lazy<HashSet<string>> LazyCompromisedPasswords = new(LoadFromEmbeddedResource);

    public Task<bool> IsCompromisedAsync(string password, CancellationToken ct) =>
        Task.FromResult(LazyCompromisedPasswords.Value.Contains(password));

    private static HashSet<string> LoadFromEmbeddedResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "MAAT.Infrastructure.Security.common-passwords.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Ressource embarquée introuvable : {resourceName}");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                passwords.Add(line.Trim());
            }
        }

        return passwords;
    }
}
