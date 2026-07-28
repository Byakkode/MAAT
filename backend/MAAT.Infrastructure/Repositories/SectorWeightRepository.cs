using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class SectorWeightRepository(MaatDbContext context) : ISectorWeightRepository
{
    public async Task<IReadOnlyDictionary<RseDomain, decimal>> GetForSectorOrDefaultAsync(string sectorCode, CancellationToken ct)
    {
        var sectorRows = await context.SectorWeights
            .Where(sw => sw.SectorCode == sectorCode)
            .ToListAsync(ct);

        if (sectorRows.Count > 0)
        {
            return sectorRows.ToDictionary(sw => sw.Domain, sw => sw.Weight);
        }

        var defaultRows = await context.SectorWeights
            .Where(sw => sw.IsDefault)
            .ToListAsync(ct);

        return defaultRows.ToDictionary(sw => sw.Domain, sw => sw.Weight);
    }
}
