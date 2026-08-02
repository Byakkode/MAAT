using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class DiagnosticRepository(MaatDbContext context, ICurrentUserContext currentUser) : IDiagnosticRepository
{
    public Task<Diagnostic?> FindByIdAsync(Guid diagnosticId, CancellationToken ct) =>
        context.Diagnostics.FirstOrDefaultAsync(d => d.Id == diagnosticId && d.CompanyId == currentUser.CompanyId, ct);

    public Task<Diagnostic?> FindInProgressForCurrentCompanyAsync(CancellationToken ct) =>
        context.Diagnostics.FirstOrDefaultAsync(
            d => d.CompanyId == currentUser.CompanyId && d.Status == DiagnosticStatus.InProgress, ct);

    public async Task<Diagnostic> CreateAsync(CancellationToken ct)
    {
        var diagnostic = new Diagnostic(currentUser.CompanyId);
        await context.Diagnostics.AddAsync(diagnostic, ct);
        return diagnostic;
    }

    public async Task<IReadOnlyList<Diagnostic>> FindAllForCurrentCompanyAsync(CancellationToken ct) =>
        await context.Diagnostics.Where(d => d.CompanyId == currentUser.CompanyId).ToListAsync(ct);

    public async Task<IReadOnlyList<Diagnostic>> FindAllCompletedForCurrentCompanyAsync(CancellationToken ct) =>
        await context.Diagnostics
            .Where(d => d.CompanyId == currentUser.CompanyId && d.Status == DiagnosticStatus.Completed)
            .OrderBy(d => d.CompletedAt)
            .ToListAsync(ct);

    public Task<int> DeleteAllForCurrentCompanyAsync(CancellationToken ct) =>
        context.Diagnostics.Where(d => d.CompanyId == currentUser.CompanyId).ExecuteDeleteAsync(ct);
}
