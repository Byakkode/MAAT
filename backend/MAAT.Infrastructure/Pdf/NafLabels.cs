namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 4 : libellé humain du code NAF sur la page de garde
// (« 6201Z — Programmation informatique »). Source : Pdf/naf-labels.tsv, copie de la liste
// INSEE déjà utilisée par le formulaire d'inscription (frontend/src/constants/nafCodes.ts) —
// NafLabelsConsistencyTests vérifie que les deux fichiers ne divergent pas. Ressource
// embarquée, lue une fois : aucune I/O disque au moment de la génération (section 3).
public static class NafLabels
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LabelsByCode = new(Load);

    // Null pour un code absent de la liste : le rapport affiche alors le code seul plutôt
    // qu'un libellé inventé.
    public static string? For(string sectorCode) =>
        LabelsByCode.Value.TryGetValue(sectorCode.Trim().ToUpperInvariant(), out var label) ? label : null;

    internal static IReadOnlyDictionary<string, string> All => LabelsByCode.Value;

    private static IReadOnlyDictionary<string, string> Load()
    {
        var assembly = typeof(NafLabels).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".naf-labels.tsv", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Ressource embarquée illisible : {resourceName}.");
        using var reader = new StreamReader(stream);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('\t');
            labels[line[..separator]] = line[(separator + 1)..];
        }

        return labels;
    }
}
