using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class VsmeStatementRepository(MaatDbContext context) : IVsmeStatementRepository
{
    public Task<VsmeStatement?> FindAsync(Guid companyId, int year, CancellationToken ct) =>
        context.VsmeStatements.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Year == year, ct);

    public Task<List<int>> GetYearsAsync(Guid companyId, CancellationToken ct) =>
        context.VsmeStatements
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.Year)
            .OrderByDescending(y => y)
            .ToListAsync(ct);

    public Task<List<VsmeStatement>> ListAsync(Guid companyId, CancellationToken ct) =>
        context.VsmeStatements
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Year)
            .ToListAsync(ct);

    public async Task AddAsync(VsmeStatement statement, CancellationToken ct) =>
        await context.VsmeStatements.AddAsync(statement, ct);
}
