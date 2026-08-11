using MAAT.Application.DTOs;

namespace MAAT.Application.Interfaces;

// docs/specs/rapport-pdf.md : implémentation dans MAAT.Infrastructure/Pdf (QuestPDF +
// SkiaSharp) — cette couche ne connaît ni l'une ni l'autre. Pur : mêmes ReportData en entrée
// produisent toujours les mêmes octets en sortie (section 3, déterminisme) ; aucune I/O,
// aucune horloge système à l'intérieur de l'implémentation.
public interface IReportGenerator
{
    byte[] Generate(ReportData data);
}
