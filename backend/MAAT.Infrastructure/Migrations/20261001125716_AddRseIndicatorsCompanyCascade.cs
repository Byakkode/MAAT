using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRseIndicatorsCompanyCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sans clé étrangère jusqu'ici, la suppression d'une entreprise (droit à l'effacement,
            // auth-securite-rgpd.md section 6) laissait ses indicateurs derrière elle. Ces lignes
            // orphelines n'appartiennent plus à personne et empêcheraient de poser la contrainte :
            // elles sont purgées d'abord. Irréversible, et c'est voulu : le Down ne les recrée pas.
            migrationBuilder.Sql(
                "DELETE FROM rse_indicators WHERE company_id NOT IN (SELECT id FROM companies);");

            migrationBuilder.AddForeignKey(
                name: "FK_rse_indicators_companies_company_id",
                table: "rse_indicators",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rse_indicators_companies_company_id",
                table: "rse_indicators");
        }
    }
}
