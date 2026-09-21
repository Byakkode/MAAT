namespace MAAT.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    // Positionné via GitHub:Token (appsettings.Development.json) ou GitHub__Token (variable
    // d'environnement en production). Ne jamais committer un vrai token ici.
    public string? Token { get; init; }

    public string Owner { get; init; } = "Byakkode";
    public string Repo { get; init; } = "MAAT";
}
