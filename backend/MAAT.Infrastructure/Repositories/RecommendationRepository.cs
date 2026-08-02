using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class RecommendationRepository(MaatDbContext context) : IRecommendationRepository
{
    public async Task<IReadOnlyList<Recommendation>> FindAllActiveAsync(CancellationToken ct) =>
        await context.Recommendations.Where(r => r.IsActive).ToListAsync(ct);

    public Task<Recommendation?> FindByCodeAsync(string code, CancellationToken ct) =>
        context.Recommendations.FirstOrDefaultAsync(r => r.Code == code, ct);
}
