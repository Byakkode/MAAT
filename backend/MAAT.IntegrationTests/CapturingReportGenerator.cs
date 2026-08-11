using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, cas 12 à 18 : QuestPDF/SkiaSharp dessine le texte par index de
// glyphe (CID), pas par caractère ASCII — une recherche de sous-chaîne dans les octets d'un
// PDF réel ne peut donc pas vérifier son contenu affiché sans un analyseur PDF dédié. Ce faux
// capture le ReportData assemblé par DiagnosticService.GenerateReportAsync (le contrat exact
// que le générateur promet de rendre fidèlement — vérifié séparément par
// QuestPdfReportGeneratorTests/le rendu manuel) sans passer par le rendu PDF lui-même :
// l'assertion porte alors directement sur les valeurs et l'ordre transmis, ce qui est plus
// précis qu'une extraction de texte l'aurait été, pour un coût d'infrastructure nul.
public sealed class CapturingReportGenerator : IReportGenerator
{
    public ReportData? LastData { get; private set; }

    public byte[] Generate(ReportData data)
    {
        LastData = data;
        return "%PDF-1.7\n%%EOF"u8.ToArray();
    }
}
