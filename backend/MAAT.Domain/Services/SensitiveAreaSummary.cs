namespace MAAT.Domain.Services;

// ADR 0014 : catégories de zones sensibles pour la biodiversité interrogées automatiquement,
// au sens de la norme volontaire (annexe A, « biodiversity-sensitive area »).
public enum SensitiveAreaKind
{
    NaturaHabitats,
    NaturaBirds,
    NatureReserve,
    NationalPark,
    HuntingWildlifeReserve,
    Znieff1,
}

public sealed record SensitiveArea(string Name, SensitiveAreaKind Kind);

// Met en forme les zones trouvées autour d'un site, pour l'écran et le rapport :
// « Massif de Fontainebleau (Natura 2000 Habitats, Natura 2000 Oiseaux, ZNIEFF 1) ». Une même
// zone protégée à plusieurs titres n'apparaît qu'une fois. Pur et déterministe : même liste,
// même texte, quel que soit l'ordre des réponses du service (rapport-pdf.md, section 3).
public static class SensitiveAreaSummary
{
    // Au-delà, « et N autres » : le rapport nomme les zones, il ne les inventorie pas.
    public const int MaxNamedAreas = 4;

    public static string? Format(IReadOnlyList<SensitiveArea> areas)
    {
        if (areas.Count == 0)
        {
            return null;
        }

        var groups = areas
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .GroupBy(a => Normalize(a.Name))
            .Select(g => new
            {
                // Les ZNIEFF sont publiées en capitales : on garde le nom d'une autre couche
                // quand il existe, sinon le nom tel quel.
                Name = g.OrderBy(a => a.Name == a.Name.ToUpperInvariant() ? 1 : 0).ThenBy(a => a.Kind).First().Name.Trim(),
                Kinds = g.Select(a => a.Kind).Distinct().Order().ToList(),
            })
            .OrderBy(g => g.Kinds[0])
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();

        if (groups.Count == 0)
        {
            return null;
        }

        var named = groups.Take(MaxNamedAreas).Select(g => $"{g.Name} ({string.Join(", ", g.Kinds.Select(Label))})");
        var text = string.Join(" ; ", named);
        var others = groups.Count - MaxNamedAreas;
        return others > 0 ? $"{text} ; et {others} autre{(others > 1 ? "s" : string.Empty)}" : text;
    }

    public static string Label(SensitiveAreaKind kind) => kind switch
    {
        SensitiveAreaKind.NaturaHabitats => "Natura 2000 Habitats",
        SensitiveAreaKind.NaturaBirds => "Natura 2000 Oiseaux",
        SensitiveAreaKind.NatureReserve => "réserve naturelle",
        SensitiveAreaKind.NationalPark => "parc national",
        SensitiveAreaKind.HuntingWildlifeReserve => "réserve de chasse et de faune sauvage",
        _ => "ZNIEFF 1",
    };

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
