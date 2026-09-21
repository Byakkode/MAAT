using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItemProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "action_item_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_item_progress", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_item_progress_diagnostic_id_recommendation_code",
                table: "action_item_progress",
                columns: new[] { "diagnostic_id", "recommendation_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_item_progress");
        }
    }
}
