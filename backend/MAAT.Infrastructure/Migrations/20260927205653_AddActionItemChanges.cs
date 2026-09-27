using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItemChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "action_item_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    field = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_item_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_action_item_changes_diagnostics_diagnostic_id",
                        column: x => x.diagnostic_id,
                        principalTable: "diagnostics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_action_item_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_item_changes_changed_by_user_id",
                table: "action_item_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_action_item_changes_diagnostic_id_recommendation_code_chang~",
                table: "action_item_changes",
                columns: new[] { "diagnostic_id", "recommendation_code", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_item_changes");
        }
    }
}
