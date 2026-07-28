using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 3 : les jetons expirés ou révoqués depuis
// plus de 30 jours doivent être purgés ; les autres (dont les jetons révoqués récemment,
// nécessaires à la détection de réutilisation) doivent être conservés.
[Collection("Postgres")]
public class RefreshTokenPurgeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Purge_supprime_les_jetons_expires_ou_revoques_depuis_plus_de_30_jours_et_conserve_les_autres()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - TimeSpan.FromDays(30);

        var company = new Company($"Entreprise Purge {Guid.NewGuid():N}", "6201Z", CompanySizeRange.Micro, "Île-de-France");
        var user = new User($"purge-{Guid.NewGuid():N}@example.test", "hash-non-utilise", company.Id);

        var expireDepuisLongtemps = new RefreshToken(user.Id, $"purge-test-{Guid.NewGuid():N}-expire-vieux", now.AddDays(-40));
        var revoqueDepuisLongtemps = new RefreshToken(user.Id, $"purge-test-{Guid.NewGuid():N}-revoque-vieux", now.AddDays(10))
        {
            RevokedAt = now.AddDays(-31),
        };
        var expireRecemment = new RefreshToken(user.Id, $"purge-test-{Guid.NewGuid():N}-expire-recent", now.AddDays(-1));
        var actif = new RefreshToken(user.Id, $"purge-test-{Guid.NewGuid():N}-actif", now.AddDays(5));

        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.Companies.Add(company);
            seedContext.Users.Add(user);
            seedContext.RefreshTokens.AddRange(expireDepuisLongtemps, revoqueDepuisLongtemps, expireRecemment, actif);
            await seedContext.SaveChangesAsync();
        }

        int purgedCount;
        await using (var context = fixture.CreateContext())
        {
            var repository = new RefreshTokenRepository(context);
            purgedCount = await repository.PurgeExpiredOrRevokedBeforeAsync(cutoff, CancellationToken.None);
        }

        Assert.Equal(2, purgedCount);

        await using var verifyContext = fixture.CreateContext();
        var remainingIds = await verifyContext.RefreshTokens.AsNoTracking().Select(rt => rt.Id).ToListAsync();

        Assert.DoesNotContain(expireDepuisLongtemps.Id, remainingIds);
        Assert.DoesNotContain(revoqueDepuisLongtemps.Id, remainingIds);
        Assert.Contains(expireRecemment.Id, remainingIds);
        Assert.Contains(actif.Id, remainingIds);
    }
}
