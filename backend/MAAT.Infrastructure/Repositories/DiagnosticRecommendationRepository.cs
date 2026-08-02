using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class DiagnosticRecommendationRepository(MaatDbContext context, ICurrentUserContext currentUser)
    : IDiagnosticRecommendationRepository
{
    public async Task<IReadOnlyList<DiagnosticRecommendation>> FindAllForCurrentCompanyAsync(CancellationToken ct) =>
        await context.DiagnosticRecommendations
            .Where(dr => context.Diagnostics.Any(d => d.Id == dr.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .ToListAsync(ct);

    public async Task AddRangeAsync(IReadOnlyList<DiagnosticRecommendation> recommendations, CancellationToken ct) =>
        await context.DiagnosticRecommendations.AddRangeAsync(recommendations, ct);

    // La jointure vers recommendations n'applique aucun filtre sur is_active (cas 14).
    public async Task<IReadOnlyList<DiagnosticRecommendationView>> FindAllForDiagnosticAsync(Guid diagnosticId, CancellationToken ct) =>
        await (
            from dr in context.DiagnosticRecommendations
            where dr.DiagnosticId == diagnosticId
            where context.Diagnostics.Any(d => d.Id == dr.DiagnosticId && d.CompanyId == currentUser.CompanyId)
            join r in context.Recommendations on dr.RecommendationId equals r.Id
            orderby dr.PriorityRank
            select new DiagnosticRecommendationView(
                r.Code,
                r.ActionText,
                r.DetailText,
                r.Domain,
                r.EffortLevel,
                r.ImpactPoints,
                dr.PriorityRank,
                dr.IsCompleted,
                dr.CompletedAt))
            .ToListAsync(ct);

    public Task<DiagnosticRecommendation?> FindAsync(Guid diagnosticId, Guid recommendationId, CancellationToken ct) =>
        context.DiagnosticRecommendations
            .Where(dr => dr.DiagnosticId == diagnosticId && dr.RecommendationId == recommendationId)
            .Where(dr => context.Diagnostics.Any(d => d.Id == dr.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .FirstOrDefaultAsync(ct);
}
