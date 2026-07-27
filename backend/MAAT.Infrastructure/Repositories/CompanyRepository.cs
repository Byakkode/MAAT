using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;

namespace MAAT.Infrastructure.Repositories;

public class CompanyRepository(MaatDbContext context) : ICompanyRepository
{
    public async Task AddAsync(Company company, CancellationToken ct) =>
        await context.Companies.AddAsync(company, ct);
}
