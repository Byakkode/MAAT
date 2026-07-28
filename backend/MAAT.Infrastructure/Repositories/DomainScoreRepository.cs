using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class DomainScoreRepository(MaatDbContext context, ICurrentUserContext currentUser) : IDomainScoreRepository
{
    public Task<DomainScore?> FindAsync(Guid diagnosticId, RseDomain domain, CancellationToken ct) =>
        context.DomainScores
            .Where(ds => ds.DiagnosticId == diagnosticId && ds.Domain == domain)
            .Where(ds => context.Diagnostics.Any(d => d.Id == ds.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DomainScore>> FindAllForCurrentCompanyAsync(CancellationToken ct) =>
        await context.DomainScores
            .Where(ds => context.Diagnostics.Any(d => d.Id == ds.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .ToListAsync(ct);

    public async Task AddAsync(DomainScore domainScore, CancellationToken ct) =>
        await context.DomainScores.AddAsync(domainScore, ct);
}
