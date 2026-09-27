using MAAT.Application.DTOs;
using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// docs/specs/recommandations.md, section 4 bis. Le diagnostic est toujours vérifié comme
// appartenant à l'entreprise du principal (IDiagnosticRepository.FindByIdAsync) avant tout
// appel : ce dépôt ne filtre que par diagnostic et code.
public interface IActionItemChangeRepository
{
    // La dernière ligne de cette action, pour le regroupement des notes.
    Task<ActionItemChange?> FindLatestAsync(Guid diagnosticId, string recommendationCode, CancellationToken ct);

    void Add(ActionItemChange change);

    // Du plus récent au plus ancien, avec l'adresse de l'auteur (null si le compte a été
    // supprimé depuis).
    Task<IReadOnlyList<ActionItemChangeView>> ListAsync(Guid diagnosticId, string recommendationCode, CancellationToken ct);
}
