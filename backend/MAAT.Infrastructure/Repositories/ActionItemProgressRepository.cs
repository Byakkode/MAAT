using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class ActionItemProgressRepository(MaatDbContext context) : IActionItemProgressRepository
{
    public async Task<Dictionary<string, ActionItemProgress>> GetMapByDiagnosticAsync(
        Guid diagnosticId, CancellationToken ct) =>
        await context.ActionItemProgresses
            .Where(p => p.DiagnosticId == diagnosticId)
            .ToDictionaryAsync(p => p.RecommendationCode, ct);

    public async Task<ActionItemProgress?> GetByDiagnosticAndCodeAsync(
        Guid diagnosticId, string code, CancellationToken ct) =>
        await context.ActionItemProgresses
            .FirstOrDefaultAsync(p => p.DiagnosticId == diagnosticId && p.RecommendationCode == code, ct);

    public void Add(ActionItemProgress progress) => context.ActionItemProgresses.Add(progress);

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
