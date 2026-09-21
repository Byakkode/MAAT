using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IActionItemProgressRepository
{
    Task<Dictionary<string, ActionItemProgress>> GetMapByDiagnosticAsync(Guid diagnosticId, CancellationToken ct);
    Task<ActionItemProgress?> GetByDiagnosticAndCodeAsync(Guid diagnosticId, string code, CancellationToken ct);
    void Add(ActionItemProgress progress);
    Task SaveAsync(CancellationToken ct);
}
