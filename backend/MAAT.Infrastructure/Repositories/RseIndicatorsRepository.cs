using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class RseIndicatorsRepository(MaatDbContext context) : IRseIndicatorsRepository
{
    public async Task<RseIndicators?> GetByCompanyAndYearAsync(
        Guid companyId, int year, CancellationToken ct) =>
        await context.RseIndicators
            .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Year == year, ct);

    public async Task<List<int>> GetYearsByCompanyAsync(Guid companyId, CancellationToken ct) =>
        await context.RseIndicators
            .Where(r => r.CompanyId == companyId)
            .Select(r => r.Year)
            .OrderByDescending(y => y)
            .ToListAsync(ct);

    public void Add(RseIndicators record) => context.RseIndicators.Add(record);

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
