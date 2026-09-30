using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVsmeReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "collective_bargaining_pct",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "electricity_non_renewable_mwh",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "electricity_renewable_mwh",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "female_employees",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "fuels_non_renewable_mwh",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "fuels_renewable_mwh",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "gender_pay_gap_pct",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "hazardous_waste_tons",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "hours_worked",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "male_employees",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "non_hazardous_waste_tons",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "other_gender_employees",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "permanent_employees",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recordable_accidents",
                table: "rse_indicators",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "scope1_tco2e",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "scope2_location_tco2e",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "temporary_employees",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "water_consumption_stress_m3",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "water_withdrawal_m3",
                table: "rse_indicators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "work_fatalities",
                table: "rse_indicators",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    geocoded_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    in_or_near_sensitive_area = table.Column<bool>(type: "boolean", nullable: true),
                    sensitive_area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_sites", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_sites_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vsme_statements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    reporting_basis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    legal_form = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_assets_eur = table.Column<double>(type: "double precision", nullable: true),
                    primary_country = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    employee_count_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    omitted_disclosures = table.Column<string[]>(type: "text[]", nullable: false),
                    has_practices = table.Column<bool>(type: "boolean", nullable: true),
                    has_policies = table.Column<bool>(type: "boolean", nullable: true),
                    policies_public = table.Column<bool>(type: "boolean", nullable: true),
                    has_future_initiatives = table.Column<bool>(type: "boolean", nullable: true),
                    has_targets = table.Column<bool>(type: "boolean", nullable: true),
                    practices_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    covered_topics = table.Column<string[]>(type: "text[]", nullable: false),
                    pollution_reporting_applicable = table.Column<bool>(type: "boolean", nullable: true),
                    pollution_report_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    circular_economy_applied = table.Column<bool>(type: "boolean", nullable: true),
                    circular_economy_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    material_flows_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    minimum_wage_met = table.Column<bool>(type: "boolean", nullable: true),
                    corruption_convictions = table.Column<int>(type: "integer", nullable: true),
                    corruption_fines_eur = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    certifications = table.Column<string>(type: "jsonb", nullable: true),
                    employees_by_country = table.Column<string>(type: "jsonb", nullable: true),
                    pollutants = table.Column<string>(type: "jsonb", nullable: true),
                    subsidiaries = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vsme_statements", x => x.id);
                    table.ForeignKey(
                        name: "FK_vsme_statements_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_sites_company_id",
                table: "company_sites",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_vsme_statements_company_id_year",
                table: "vsme_statements",
                columns: new[] { "company_id", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_sites");

            migrationBuilder.DropTable(
                name: "vsme_statements");

            migrationBuilder.DropColumn(
                name: "collective_bargaining_pct",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "electricity_non_renewable_mwh",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "electricity_renewable_mwh",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "female_employees",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "fuels_non_renewable_mwh",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "fuels_renewable_mwh",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "gender_pay_gap_pct",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "hazardous_waste_tons",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "hours_worked",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "male_employees",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "non_hazardous_waste_tons",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "other_gender_employees",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "permanent_employees",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "recordable_accidents",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "scope1_tco2e",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "scope2_location_tco2e",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "temporary_employees",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "water_consumption_stress_m3",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "water_withdrawal_m3",
                table: "rse_indicators");

            migrationBuilder.DropColumn(
                name: "work_fatalities",
                table: "rse_indicators");
        }
    }
}
