using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSensitiveAreaDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detected_sensitive_areas",
                table: "company_sites",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sensitive_areas_checked_at",
                table: "company_sites",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "detected_sensitive_areas",
                table: "company_sites");

            migrationBuilder.DropColumn(
                name: "sensitive_areas_checked_at",
                table: "company_sites");
        }
    }
}
