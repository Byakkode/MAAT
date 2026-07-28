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
}
