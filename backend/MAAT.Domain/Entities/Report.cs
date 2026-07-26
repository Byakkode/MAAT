using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class Report
{
    public Guid Id { get; private set; }
    public Guid DiagnosticId { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
    public DateTimeOffset GeneratedAt { get; private set; }
    public Guid GeneratedByUserId { get; set; }

    private Report()
    {
    }

    public Report(Guid diagnosticId, Guid generatedByUserId, ReportFormat format = ReportFormat.Pdf)
    {
        Id = Guid.NewGuid();
        DiagnosticId = diagnosticId;
        GeneratedByUserId = generatedByUserId;
        Format = format;
        GeneratedAt = DateTimeOffset.UtcNow;
    }
}
