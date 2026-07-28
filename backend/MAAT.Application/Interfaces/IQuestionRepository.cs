using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// Question est une table de référence, pas une donnée d'entreprise (modele-donnees.md) :
// aucun filtrage par company_id, contrairement aux dépôts de docs/specs/auth-securite-rgpd.md
// section 4.
public interface IQuestionRepository
{
    Task<IReadOnlyList<Question>> FindAllActiveAsync(CancellationToken ct);

    Task<Question?> FindByCodeAsync(string code, CancellationToken ct);
}
