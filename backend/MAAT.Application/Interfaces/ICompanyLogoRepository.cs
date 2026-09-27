using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

// docs/specs/rapport-pdf.md, section 7. L'entreprise est toujours celle du principal
// authentifié (currentUser.CompanyId, jamais une valeur reçue du client) : même principe que
// ISubscriptionRepository.
public interface ICompanyLogoRepository
{
    Task<CompanyLogo?> FindByCompanyIdAsync(Guid companyId, CancellationToken ct);

    Task AddAsync(CompanyLogo logo, CancellationToken ct);

    void Remove(CompanyLogo logo);
}
