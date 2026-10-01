using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class CompanySiteRepository(MaatDbContext context) : ICompanySiteRepository
{
    public Task<List<CompanySite>> ListAsync(Guid companyId, CancellationToken ct) =>
        context.CompanySites
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public Task<CompanySite?> FindAsync(Guid companyId, Guid siteId, CancellationToken ct) =>
        context.CompanySites.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Id == siteId, ct);

    public Task<int> CountAsync(Guid companyId, CancellationToken ct) =>
        context.CompanySites.CountAsync(s => s.CompanyId == companyId, ct);

    public async Task AddAsync(CompanySite site, CancellationToken ct) =>
        await context.CompanySites.AddAsync(site, ct);

    public void Remove(CompanySite site) => context.CompanySites.Remove(site);
}
