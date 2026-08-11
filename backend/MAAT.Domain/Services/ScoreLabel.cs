namespace MAAT.Domain.Services;

// docs/specs/dashboard.md, section 2 : ces seuils sont des constantes du frontend
// (frontend/src/constants/scoreLabels.ts) et doivent apparaître à l'identique dans le rapport
// PDF (rapport-pdf.md, section 4) — un même score ne peut pas être qualifié différemment
// selon le support. Réplique exacte de la table frontend, tenue à jour manuellement des deux
// côtés faute d'une source commune entre TypeScript et C# dans ce projet.
public static class ScoreLabel
{
    private static readonly (int Min, int Max, string Label)[] Thresholds =
    [
        (0, 24, "Démarche à initier"),
        (25, 49, "Premiers pas engagés"),
        (50, 69, "Démarche structurée"),
        (70, 84, "Démarche avancée"),
        (85, 100, "Démarche exemplaire"),
    ];

    // Prend un score déjà arrondi pour l'affichage (ScoringService.RoundForDisplay) : les
    // tranches ci-dessus sont définies en entiers, comme côté frontend.
    public static string For(int roundedScore)
    {
        foreach (var (min, max, label) in Thresholds)
        {
            if (roundedScore >= min && roundedScore <= max)
            {
                return label;
            }
        }

        return Thresholds[^1].Label;
    }
}
