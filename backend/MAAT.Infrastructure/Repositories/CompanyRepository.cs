using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class CompanyRepository(MaatDbContext context) : ICompanyRepository
{
    public async Task AddAsync(Company company, CancellationToken ct) =>
        await context.Companies.AddAsync(company, ct);

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct) =>
        await context.Companies.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
}
