namespace MAAT.Infrastructure.Pdf;

// Noms de famille explicites enregistrés par QuestPdfBootstrapper — un par fichier
// embarqué, plutôt que de compter sur la résolution automatique de graisse de QuestPDF
// (.Bold()/.SemiBold()), qui dépend de métadonnées de police non uniformes d'une famille à
// l'autre. docs/specs/rapport-pdf.md, section 5 : correspondance avec la charte-maat —
// Poppins Bold pour les titres (H1/H2), Poppins SemiBold pour les sous-titres (H3/H4), Inter
// pour le corps de texte, Inter Light pour les légendes.
public static class FontFamilies
{
    public const string Poppins = "Poppins";
    public const string PoppinsMedium = "Poppins Medium";
    public const string PoppinsSemiBold = "Poppins SemiBold";
    public const string PoppinsBold = "Poppins Bold";
    public const string Inter = "Inter";
    public const string InterLight = "Inter Light";
}
