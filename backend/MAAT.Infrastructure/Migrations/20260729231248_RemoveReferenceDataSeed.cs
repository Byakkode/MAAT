using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    // docs/specs/modele-donnees.md : « ne jamais supprimer une Question » — ni, par
    // extension, une Recommendation ou une ligne SectorWeight, toutes potentiellement
    // référencées par des Response/DiagnosticRecommendation existantes via une contrainte
    // RESTRICT. Cette migration retire uniquement le HasData du modèle (Question,
    // Recommendation, SectorWeight ne sont plus semées par migration, voir
    // ReferenceDataSeeder) : elle ne porte donc aucune opération sur les données déjà
    // présentes — celles-ci restent en base telles quelles, et ReferenceDataSeeder les
    // met à jour par upsert sur code (sector_code+domain pour SectorWeight) au prochain
    // chargement du seed CSV, sans jamais les supprimer ni les recréer.
    //
    // Voir RemoveReferenceDataSeedTests pour le scénario qui vérifie que cette migration
    // s'applique sans erreur sur une base contenant déjà des Response référençant une
    // question seedée par une migration antérieure — le scénario de production.
    /// <inheritdoc />
    public partial class RemoveReferenceDataSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
