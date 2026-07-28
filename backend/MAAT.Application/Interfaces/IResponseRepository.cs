using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Response n'a pas de company_id direct : le filtrage passe par une jointure vers
// Diagnostic (voir IDiagnosticRepository pour le principe général).
public interface IResponseRepository
{
    Task<Response?> FindByIdAsync(Guid responseId, CancellationToken ct);

    // Export RGPD (section 6, cas 18).
    Task<IReadOnlyList<Response>> FindAllForCurrentCompanyAsync(CancellationToken ct);

    // Sauvegarde automatique (questionnaire.md, section 4) : diagnosticId doit avoir été
    // validé au préalable par IDiagnosticRepository.FindByIdAsync (donc déjà cloisonné) —
    // ces deux méthodes filtrent quand même indépendamment par jointure, en défense en
    // profondeur, comme le reste des dépôts à portée d'entreprise.
    Task<Response?> FindByDiagnosticAndQuestionAsync(Guid diagnosticId, Guid questionId, CancellationToken ct);

    Task<IReadOnlyList<Response>> FindAllForDiagnosticAsync(Guid diagnosticId, CancellationToken ct);

    Task AddAsync(Response response, CancellationToken ct);
}
