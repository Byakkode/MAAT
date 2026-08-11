using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 1 et 5. Appelée explicitement au démarrage
// (Program.cs, docs/adr/0006-licence-questpdf.md) : sans l'appel à QuestPDF.Settings.License,
// la bibliothèque lève une exception au premier document généré — un échec de runtime qu'on
// préfère détecter au démarrage plutôt qu'au premier téléchargement réel. Polices enregistrées
// au même endroit et au même moment : le premier rapport généré ne doit jamais dépendre de
// polices système absentes du VPS cible (section 5).
public static class QuestPdfBootstrapper
{
    private static readonly IReadOnlyDictionary<string, string> FontFamilyByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Poppins-Regular.ttf"] = FontFamilies.Poppins,
        ["Poppins-Medium.ttf"] = FontFamilies.PoppinsMedium,
        ["Poppins-SemiBold.ttf"] = FontFamilies.PoppinsSemiBold,
        ["Poppins-Bold.ttf"] = FontFamilies.PoppinsBold,
        ["Inter-Regular.ttf"] = FontFamilies.Inter,
        ["Inter-Light.ttf"] = FontFamilies.InterLight,
    };

    private static bool _configured;
    private static readonly object Lock = new();

    public static void Configure()
    {
        lock (Lock)
        {
            if (_configured)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            RegisterEmbeddedFonts();
            _configured = true;
        }
    }

    private static void RegisterEmbeddedFonts()
    {
        var assembly = typeof(QuestPdfBootstrapper).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var (fileName, familyName) in FontFamilyByFileName)
        {
            var resourceName = resourceNames.FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Police embarquée introuvable : {fileName} (attendue sous Pdf/Fonts/, voir MAAT.Infrastructure.csproj).");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Ressource embarquée illisible : {resourceName}.");

            FontManager.RegisterFontWithCustomName(familyName, stream);
        }
    }
}
