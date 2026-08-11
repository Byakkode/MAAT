using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Report n'a pas de company_id direct : le filtrage passe par une jointure vers
// Diagnostic (voir IDiagnosticRepository pour le principe général).
public interface IReportRepository
{
    Task<Report?> FindByIdAsync(Guid reportId, CancellationToken ct);

    // Export RGPD (section 6, cas 18).
    Task<IReadOnlyList<Report>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // docs/specs/rapport-pdf.md, section 2 : journal d'audit écrit après la production
    // réussie du document, jamais avant (cas 7 et 8).
    Task AddAsync(Report report, CancellationToken ct);
}
