using MAAT.Domain.Enums;
using QuestPDF.Infrastructure;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 5 et skill charte-maat : toutes les couleurs du rapport,
// en un seul endroit. Chaque valeur est un token de la charte (frontend/src/index.css ou
// docs/specs/charte-maat-v2.md, section 6) — aucune teinte inventée ici. Les fonds de badge
// et de carte sont des variantes transparentes de ces tokens (WithAlpha), comme le fait le
// frontend avec `bg-blue-maat/10`.
public static class ReportTheme
{
    public static readonly Color Ink = Color.FromHex("#1E1E2D");          // --maat-ink
    public static readonly Color InkMuted = Color.FromHex("#5B6472");     // --maat-ink-muted (contraste AA sur blanc)
    public static readonly Color Border = Color.FromHex("#E5E7EB");       // --color-border
    public static readonly Color BorderStrong = Color.FromHex("#CBD5E1"); // --color-border-strong
    public static readonly Color Surface = Color.FromHex("#FFFFFF");
    public static readonly Color Background = Color.FromHex("#F8F9FC");   // --maat-bg
    public static readonly Color Sidebar = Color.FromHex("#0F172A");      // --color-sidebar

    public static readonly Color Blue = Color.FromHex("#1565FF");         // --color-blue-maat
    public static readonly Color BlueText = Color.FromHex("#0D4FD6");     // --color-blue-maat-text
    public static readonly Color Green = Color.FromHex("#29CC6A");        // --color-green-maat
    public static readonly Color GreenText = Color.FromHex("#12734A");    // --color-green-maat-text
    public static readonly Color Orange = Color.FromHex("#FFA64D");       // --color-orange
    public static readonly Color AmberText = Color.FromHex("#B96D0A");    // --color-amber
    public static readonly Color Red = Color.FromHex("#E53935");          // --color-red

    public static readonly Color KpiBlue = Color.FromHex("#EFF6FF");      // --color-kpi-blue
    public static readonly Color KpiGreen = Color.FromHex("#F0FDF4");     // --color-kpi-green
    public static readonly Color KpiAmber = Color.FromHex("#FFFBEB");     // --color-kpi-amber

    // Une couleur par domaine, à ne jamais réattribuer (charte-maat, « Graphiques ») : mêmes
    // valeurs que --color-chart-* de frontend/src/index.css. RadarChartRenderer en dérive ses
    // couleurs SkiaSharp, et DomainColorConsistencyTests compare les deux fichiers.
    public static readonly IReadOnlyDictionary<RseDomain, string> DomainHex = new Dictionary<RseDomain, string>
    {
        [RseDomain.Environmental] = "#29CC6A",
        [RseDomain.Social] = "#1E88E5",
        [RseDomain.Ethics] = "#7E57C2",
        [RseDomain.Procurement] = "#FFB74D",
        [RseDomain.Governance] = "#42A5F5",
    };

    public static Color DomainColor(RseDomain domain) => Color.FromHex(DomainHex[domain]);

    // Libellés courts pour les pastilles et les colonnes étroites, où le libellé complet de
    // RseDomainLabels passerait à la ligne.
    public static string DomainShortLabel(RseDomain domain) => domain switch
    {
        RseDomain.Environmental => "Environnement",
        RseDomain.Social => "Social",
        RseDomain.Ethics => "Éthique",
        RseDomain.Procurement => "Achats",
        RseDomain.Governance => "Gouvernance",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };

    // Mêmes libellés et mêmes couleurs que l'écran Plan d'actions
    // (frontend/src/pages/PlanActionsPage.tsx, STATUS_META).
    public static (string Label, Color Text, Color Fill) Status(ActionItemStatus status) => status switch
    {
        ActionItemStatus.Planned => ("Planifié", InkMuted, Background),
        ActionItemStatus.InProgress => ("En cours", BlueText, Blue.WithAlpha(0.10f)),
        ActionItemStatus.Blocked => ("Bloqué", AmberText, Orange.WithAlpha(0.14f)),
        ActionItemStatus.Done => ("Terminé", GreenText, Green.WithAlpha(0.12f)),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    // Couleur de barre de la répartition des statuts : plus saturée que le fond de badge.
    public static Color StatusBar(ActionItemStatus status) => status switch
    {
        ActionItemStatus.Planned => BorderStrong,
        ActionItemStatus.InProgress => Blue,
        ActionItemStatus.Blocked => Orange,
        ActionItemStatus.Done => Green,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
