using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRseIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rse_indicators",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    co2_emissions_tons = table.Column<double>(type: "double precision", nullable: true),
                    energy_consumption_kwh = table.Column<double>(type: "double precision", nullable: true),
                    renewable_energy_pct = table.Column<double>(type: "double precision", nullable: true),
                    water_consumption_m3 = table.Column<double>(type: "double precision", nullable: true),
                    waste_tons = table.Column<double>(type: "double precision", nullable: true),
                    recycling_rate_pct = table.Column<double>(type: "double precision", nullable: true),
                    employee_count_fte = table.Column<double>(type: "double precision", nullable: true),
                    turnover_rate_pct = table.Column<double>(type: "double precision", nullable: true),
                    training_hours_per_employee = table.Column<double>(type: "double precision", nullable: true),
                    work_accident_rate = table.Column<double>(type: "double precision", nullable: true),
                    gender_equality_index = table.Column<double>(type: "double precision", nullable: true),
                    permanent_contract_pct = table.Column<double>(type: "double precision", nullable: true),
                    local_suppliers_pct = table.Column<double>(type: "double precision", nullable: true),
                    rse_assessed_suppliers_pct = table.Column<double>(type: "double precision", nullable: true),
                    active_suppliers_count = table.Column<int>(type: "integer", nullable: true),
                    revenue_eur = table.Column<double>(type: "double precision", nullable: true),
                    rse_investment_eur = table.Column<double>(type: "double precision", nullable: true),
                    export_revenue_pct = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rse_indicators", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rse_indicators_company_id_year",
                table: "rse_indicators",
                columns: new[] { "company_id", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rse_indicators");
        }
    }
}
