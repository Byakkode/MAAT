using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Dépôt à portée d'entreprise (docs/specs/auth-securite-rgpd.md, section 4) : chaque
// implémentation reçoit ICurrentUserContext par injection et filtre par ce company_id
// à la construction de la requête. Aucune méthode n'accepte de company_id en paramètre :
// il est structurellement impossible d'en fournir un autre que celui du principal.
public interface IDiagnosticRepository
{
    Task<Diagnostic?> FindByIdAsync(Guid diagnosticId, CancellationToken ct);

    Task<Diagnostic> CreateAsync(CancellationToken ct);

    // questionnaire.md, section 1 : une entreprise ne peut avoir qu'un seul diagnostic
    // InProgress à la fois — ce dépôt ne fait qu'exposer la lecture, l'invariant lui-même
    // est appliqué par DiagnosticService.CreateAsync (409 si non null).
    Task<Diagnostic?> FindInProgressForCurrentCompanyAsync(CancellationToken ct);

    // Export RGPD (section 6, cas 18).
    Task<IReadOnlyList<Diagnostic>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // Tableau de bord (dashboard.md, sections 1 et 4) : uniquement les diagnostics Completed,
    // triés du plus ancien au plus récent — Archived n'a pas de score (cas 5) et InProgress
    // est déjà couvert par FindInProgressForCurrentCompanyAsync ci-dessus (le bandeau
    // "diagnostic en cours"). Le dernier élément sert de référence pour le score courant,
    // les cinq DomainScore et le benchmark (cas 4).
    Task<IReadOnlyList<Diagnostic>> FindAllCompletedForCurrentCompanyAsync(CancellationToken ct);

    // Purge RGPD (section 6) : supprime tous les Diagnostic de l'entreprise courante.
    // Cascade DB vers Response, DomainScore, DiagnosticRecommendation et Report — donc
    // vers Report avant que DeleteAllForCompanyAsync sur User ne s'exécute (voir
    // IUserRepository).
    Task<int> DeleteAllForCurrentCompanyAsync(CancellationToken ct);
}
