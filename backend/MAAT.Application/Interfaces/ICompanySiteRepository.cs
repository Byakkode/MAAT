using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// docs/specs/norme-volontaire.md. Sites de l'entreprise du principal authentifié, dans un
// ordre stable (date de création, puis identifiant) : ils figurent dans le rapport, qui doit
// être identique à données identiques (rapport-pdf.md, section 3).
public interface ICompanySiteRepository
{
    Task<List<CompanySite>> ListAsync(Guid companyId, CancellationToken ct);

    Task<CompanySite?> FindAsync(Guid companyId, Guid siteId, CancellationToken ct);

    Task<int> CountAsync(Guid companyId, CancellationToken ct);

    Task AddAsync(CompanySite site, CancellationToken ct);

    void Remove(CompanySite site);
}
