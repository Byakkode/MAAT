using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface ICompanyRepository
{
    Task AddAsync(Company company, CancellationToken ct);
}
