using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// DomainScore n'a pas d'identifiant scalaire propre : sa clé est (diagnostic_id, domain).
// Le filtrage passe par une jointure vers Diagnostic (voir IDiagnosticRepository).
public interface IDomainScoreRepository
{
    Task<DomainScore?> FindAsync(Guid diagnosticId, RseDomain domain, CancellationToken ct);

    // Export RGPD (section 6, cas 18).
    Task<IReadOnlyList<DomainScore>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // Complétion (questionnaire.md, section 6) : diagnosticId doit avoir été validé au
    // préalable, comme pour IResponseRepository.AddAsync.
    Task AddAsync(DomainScore domainScore, CancellationToken ct);
}
