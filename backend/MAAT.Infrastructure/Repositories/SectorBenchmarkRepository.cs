using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

// Jamais de ICurrentUserContext ici (contrairement au reste de ce dossier) : voir le
// commentaire d'ISectorBenchmarkRepository, la seule exception documentée au patron de
// cloisonnement par entreprise.
public class SectorBenchmarkRepository(MaatDbContext context) : ISectorBenchmarkRepository
{
    public async Task<IReadOnlyList<decimal>> FindLatestCompletedScoresBySectorAsync(string sectorCode, CancellationToken ct)
    {
        // Regroupement en mémoire plutôt qu'en SQL (GroupBy + OrderByDescending().First()
        // dans une projection EF Core est fragile à traduire correctement) : le nombre de
        // diagnostics complétés par secteur reste modeste, une réduction côté client est ici
        // plus simple à lire et à garantir correcte qu'une requête SQL astucieuse.
        var completedInSector = await (
            from d in context.Diagnostics
            join c in context.Companies on d.CompanyId equals c.Id
            where d.Status == DiagnosticStatus.Completed && c.SectorCode == sectorCode
            select new { d.CompanyId, d.CompletedAt, d.GlobalScore })
            .ToListAsync(ct);

        return completedInSector
            .GroupBy(x => x.CompanyId)
            .Select(g => g.OrderByDescending(x => x.CompletedAt).First().GlobalScore!.Value)
            .ToList();
    }
}
