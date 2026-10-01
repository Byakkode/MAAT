using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IActionItemProgressRepository
{
    Task<Dictionary<string, ActionItemProgress>> GetMapByDiagnosticAsync(Guid diagnosticId, CancellationToken ct);
    Task<ActionItemProgress?> GetByDiagnosticAndCodeAsync(Guid diagnosticId, string code, CancellationToken ct);

    // Le suivi de tous les diagnostics de l'entreprise (export RGPD).
    Task<List<ActionItemProgress>> ListByCompanyAsync(Guid companyId, CancellationToken ct);

    void Add(ActionItemProgress progress);
    Task SaveAsync(CancellationToken ct);
}
