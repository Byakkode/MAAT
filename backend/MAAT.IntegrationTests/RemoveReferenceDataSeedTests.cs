using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MAAT.IntegrationTests;

// Scénario de production pour la migration RemoveReferenceDataSeed (voir son commentaire
// dans MAAT.Infrastructure/Migrations) : une base ayant déjà appliqué les migrations
// antérieures — donc déjà seedée par InitialCreate, avant que ce seed ne soit déplacé vers
// MAAT.Infrastructure/Seed/*.csv — et contenant une vraie Response utilisateur référençant
// une question seedée doit pouvoir appliquer RemoveReferenceDataSeed sans erreur. Avant
// correction, cette migration tentait un DeleteData sur les lignes de référence, rejeté par
// la contrainte RESTRICT de responses.question_id — exactement ce que
// docs/specs/modele-donnees.md interdit (« ne jamais supprimer une Question »).
public class RemoveReferenceDataSeedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("docker.io/library/postgres:18").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private MaatDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MaatDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new MaatDbContext(options);
    }

    // ENV-01, seedée par InitialCreate.InsertData avec ce Guid fixe (jamais rejoué par le
    // seeder CSV, qui ne s'applique qu'à des bases déjà à jour — voir ReferenceDataSeeder).
    private static readonly Guid SeededQuestionId = Guid.Parse("00000000-0000-0000-0004-000000000001");

    [Fact]
    public async Task RemoveReferenceDataSeed_s_applique_sans_erreur_sur_une_base_avec_des_reponses_sur_une_question_seedee()
    {
        await using (var context = CreateContext())
        {
            // Migre jusqu'à la migration juste avant RemoveReferenceDataSeed : reproduit
            // une base de production qui a encore le seed original inséré par
            // InitialCreate (ENV-01/02/03, pondérations sectorielles).
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync("AddSectorWeightCoverageConstraint");

            var company = new Company("Entreprise Test", "6201Z", CompanySizeRange.Micro, "Île-de-France");
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            var diagnostic = new Diagnostic(company.Id);
            context.Diagnostics.Add(diagnostic);
            await context.SaveChangesAsync();

            context.Responses.Add(new Response(diagnostic.Id, SeededQuestionId, 3));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            // Le scénario testé : appliquer la migration la plus récente (donc
            // RemoveReferenceDataSeed) ne doit lever aucune exception.
            await context.Database.MigrateAsync();
        }

        await using (var context = CreateContext())
        {
            Assert.True(await context.Questions.AnyAsync(q => q.Id == SeededQuestionId));
            Assert.True(await context.Responses.AnyAsync(r => r.QuestionId == SeededQuestionId));
        }
    }
}
