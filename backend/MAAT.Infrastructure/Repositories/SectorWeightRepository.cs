using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class SectorWeightRepository(MaatDbContext context) : ISectorWeightRepository
{
    public async Task<SectorWeightLookup> GetForSectorOrDefaultAsync(string sectorCode, CancellationToken ct)
    {
        var sectorRows = await context.SectorWeights
            .Where(sw => sw.SectorCode == sectorCode)
            .ToListAsync(ct);

        if (sectorRows.Count > 0)
        {
            return new SectorWeightLookup(sectorRows.ToDictionary(sw => sw.Domain, sw => sw.Weight), UsedDefaultFallback: false);
        }

        var defaultRows = await context.SectorWeights
            .Where(sw => sw.IsDefault)
            .ToListAsync(ct);

        return new SectorWeightLookup(defaultRows.ToDictionary(sw => sw.Domain, sw => sw.Weight), UsedDefaultFallback: true);
    }
}
