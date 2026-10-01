using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItemProgressAndSupportTicketCascades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sans clé étrangère jusqu'ici, la suppression d'une entreprise (droit à l'effacement,
            // auth-securite-rgpd.md section 6) laissait derrière elle le suivi de ses actions et ses
            // tickets de support. Ces lignes orphelines n'appartiennent plus à personne et
            // empêcheraient de poser les contraintes : elles sont purgées d'abord. Irréversible, et
            // c'est voulu : le Down ne les recrée pas.
            migrationBuilder.Sql(
                "DELETE FROM action_item_progress WHERE diagnostic_id NOT IN (SELECT id FROM diagnostics);");
            migrationBuilder.Sql(
                "DELETE FROM support_tickets WHERE company_id NOT IN (SELECT id FROM companies) OR user_id NOT IN (SELECT id FROM users);");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_user_id",
                table: "support_tickets",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_action_item_progress_diagnostics_diagnostic_id",
                table: "action_item_progress",
                column: "diagnostic_id",
                principalTable: "diagnostics",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_support_tickets_companies_company_id",
                table: "support_tickets",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_support_tickets_users_user_id",
                table: "support_tickets",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_action_item_progress_diagnostics_diagnostic_id",
                table: "action_item_progress");

            migrationBuilder.DropForeignKey(
                name: "FK_support_tickets_companies_company_id",
                table: "support_tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_support_tickets_users_user_id",
                table: "support_tickets");

            migrationBuilder.DropIndex(
                name: "IX_support_tickets_user_id",
                table: "support_tickets");
        }
    }
}
