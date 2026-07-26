using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class Diagnostic
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; set; }
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.InProgress;
    public decimal? GlobalScore { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; set; }

    private Diagnostic()
    {
    }

    public Diagnostic(Guid companyId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
