using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

[Collection("Postgres")]
public class SectorWeightSeedTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Chaque_secteur_du_seed_a_les_cinq_domaines_et_une_somme_de_poids_egale_a_un()
    {
        await using var context = fixture.CreateContext();

        var parSecteur = (await context.SectorWeights.AsNoTracking().ToListAsync())
            .GroupBy(sw => sw.SectorCode);

        Assert.NotEmpty(parSecteur);

        foreach (var secteur in parSecteur)
        {
            Assert.Equal(
                Enum.GetValues<RseDomain>().Length,
                secteur.Select(sw => sw.Domain).Distinct().Count());

            Assert.Equal(1m, secteur.Sum(sw => sw.Weight));
        }
    }

    [Fact]
    public async Task Un_seul_jeu_de_ponderation_par_defaut_existe()
    {
        await using var context = fixture.CreateContext();

        var lignesParDefaut = await context.SectorWeights
            .AsNoTracking()
            .Where(sw => sw.IsDefault)
            .ToListAsync();

        Assert.Equal(Enum.GetValues<RseDomain>().Length, lignesParDefaut.Count);
        Assert.All(lignesParDefaut, sw => Assert.Null(sw.SectorCode));
    }
}
