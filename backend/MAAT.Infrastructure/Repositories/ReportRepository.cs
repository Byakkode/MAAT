using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class ReportRepository(MaatDbContext context, ICurrentUserContext currentUser) : IReportRepository
{
    public Task<Report?> FindByIdAsync(Guid reportId, CancellationToken ct) =>
        context.Reports
            .Where(r => r.Id == reportId)
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Report>> FindAllForCurrentCompanyAsync(CancellationToken ct) =>
        await context.Reports
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .ToListAsync(ct);
}
