using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface ICompanyRepository
{
    Task AddAsync(Company company, CancellationToken ct);

    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
