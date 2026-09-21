using MAAT.Application.Interfaces;
using MAAT.Domain.Services;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class SectorWeightRepository(MaatDbContext context) : ISectorWeightRepository
{
    public async Task<SectorWeightLookup> GetForSectorOrDefaultAsync(string sectorCode, CancellationToken ct)
    {
        // Niveau 1 : code NAF exact (ex. "4941A")
        var sectorRows = await context.SectorWeights
            .Where(sw => sw.SectorCode == sectorCode)
            .ToListAsync(ct);

        if (sectorRows.Count > 0)
            return new SectorWeightLookup(sectorRows.ToDictionary(sw => sw.Domain, sw => sw.Weight), UsedDefaultFallback: false);

        // Niveau 2 : section NAF (ex. "H" pour les transports) — pondération sectorielle,
        // pas la pondération générique ; UsedDefaultFallback reste false.
        var sectionCode = NafSectionResolver.GetSection(sectorCode);
        if (sectionCode is not null)
        {
            var sectionRows = await context.SectorWeights
                .Where(sw => sw.SectorCode == sectionCode)
                .ToListAsync(ct);

            if (sectionRows.Count > 0)
                return new SectorWeightLookup(sectionRows.ToDictionary(sw => sw.Domain, sw => sw.Weight), UsedDefaultFallback: false);
        }

        // Niveau 3 : pondération par défaut 20/20/20/20/20
        var defaultRows = await context.SectorWeights
            .Where(sw => sw.IsDefault)
            .ToListAsync(ct);

        return new SectorWeightLookup(defaultRows.ToDictionary(sw => sw.Domain, sw => sw.Weight), UsedDefaultFallback: true);
    }
}
