using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// DomainScore n'a pas d'identifiant scalaire propre : sa clé est (diagnostic_id, domain).
// Le filtrage passe par une jointure vers Diagnostic (voir IDiagnosticRepository).
public interface IDomainScoreRepository
{
    Task<DomainScore?> FindAsync(Guid diagnosticId, RseDomain domain, CancellationToken ct);

    // Tableau de bord (dashboard.md, section 1) : les cinq DomainScore d'un diagnostic donné,
    // pour le radar. Domain n'a pas d'ordre imposé ici — le classement par domaine est un
    // choix d'affichage frontend, hors périmètre de ce dépôt.
    Task<IReadOnlyList<DomainScore>> FindAllForDiagnosticAsync(Guid diagnosticId, CancellationToken ct);

    // Export RGPD (section 6, cas 18).
    Task<IReadOnlyList<DomainScore>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // Complétion (questionnaire.md, section 6) : diagnosticId doit avoir été validé au
    // préalable, comme pour IResponseRepository.AddAsync.
    Task AddAsync(DomainScore domainScore, CancellationToken ct);
}
