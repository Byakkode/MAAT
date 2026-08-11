namespace MAAT.Domain.Enums;

// Réplique exacte de frontend/src/pages/RegisterPage.tsx (SIZE_RANGE_LABELS), réutilisée dans
// le rapport PDF (rapport-pdf.md, section 4, page de garde).
public static class CompanySizeRangeLabels
{
    private static readonly IReadOnlyDictionary<CompanySizeRange, string> Labels = new Dictionary<CompanySizeRange, string>
    {
        [CompanySizeRange.Micro] = "Micro-entreprise (moins de 10 salariés)",
        [CompanySizeRange.Small] = "Petite entreprise (10 à 49 salariés)",
        [CompanySizeRange.Medium] = "Moyenne entreprise (50 à 249 salariés)",
        [CompanySizeRange.Large] = "Grande entreprise (250 salariés ou plus)",
    };

    public static string For(CompanySizeRange sizeRange) => Labels[sizeRange];
}
