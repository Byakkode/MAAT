namespace MAAT.Infrastructure.Pdf;

// Images fixes du rapport, embarquées dans l'assembly comme les polices et les libellés NAF :
// aucune I/O disque au moment de la génération (rapport-pdf.md, section 3), aucun fichier à
// déployer à côté du binaire.
public static class ReportAssets
{
    private static readonly Lazy<byte[]> MaatLogoWhite = new(() => Load("maat-logo-blanc.png"));

    // Logo officiel MAAT, version blanche (symbole au-dessus du nom), fond transparent :
    // copie de frontend/src/assets/maat-logo-blanc.png, pour que le document et l'application
    // portent exactement la même marque (ReportAssetsConsistencyTests).
    public static byte[] MaatLogoWhitePng => MaatLogoWhite.Value;

    private static byte[] Load(string fileName)
    {
        var assembly = typeof(ReportAssets).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Ressource embarquée illisible : {resourceName}.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
