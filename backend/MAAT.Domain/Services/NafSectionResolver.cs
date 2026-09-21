namespace MAAT.Domain.Services;

// Résout la section NAF (lettre A–S) depuis un code NAF complet (ex. "4941A" → "H").
// Utilisé par ISectorWeightRepository pour un repli sectionnel : quand un code n'a pas
// de pondération spécifique, on essaie sa section avant de tomber sur les poids par défaut.
// Sections T (97-98) et U (99) délibérément exclues : hors-scope pour les PME françaises.
public static class NafSectionResolver
{
    public static string? GetSection(string? nafCode)
    {
        if (string.IsNullOrWhiteSpace(nafCode) || nafCode.Length < 2)
            return null;

        if (!int.TryParse(nafCode.AsSpan(0, 2), out var division))
            return null;

        return division switch
        {
            >= 1 and <= 3 => "A",   // Agriculture, sylviculture et pêche
            >= 5 and <= 9 => "B",   // Industries extractives
            >= 10 and <= 33 => "C", // Industrie manufacturière
            35 => "D",              // Production et distribution d'électricité, gaz
            >= 36 and <= 39 => "E", // Production et distribution d'eau, assainissement
            >= 41 and <= 43 => "F", // Construction
            >= 45 and <= 47 => "G", // Commerce de gros et de détail
            >= 49 and <= 53 => "H", // Transports et entreposage
            55 or 56 => "I",        // Hébergement et restauration
            >= 58 and <= 63 => "J", // Information et communication
            >= 64 and <= 66 => "K", // Activités financières et d'assurance
            68 => "L",              // Activités immobilières
            >= 69 and <= 75 => "M", // Activités spécialisées, scientifiques et techniques
            >= 77 and <= 82 => "N", // Activités de services administratifs et de soutien
            84 => "O",              // Administration publique
            85 => "P",              // Enseignement
            >= 86 and <= 88 => "Q", // Santé humaine et action sociale
            >= 90 and <= 93 => "R", // Arts, spectacles et activités récréatives
            >= 94 and <= 96 => "S", // Autres activités de services
            _ => null,
        };
    }
}
