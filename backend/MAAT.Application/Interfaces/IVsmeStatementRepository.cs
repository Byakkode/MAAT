using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// docs/specs/norme-volontaire.md. L'entreprise est toujours celle du principal authentifié
// (currentUser.CompanyId), jamais une valeur reçue du client.
public interface IVsmeStatementRepository
{
    Task<VsmeStatement?> FindAsync(Guid companyId, int year, CancellationToken ct);

    Task<List<int>> GetYearsAsync(Guid companyId, CancellationToken ct);

    Task AddAsync(VsmeStatement statement, CancellationToken ct);
}
