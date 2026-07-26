using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    siret = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sector_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    size_range = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    help_text = table.Column<string>(type: "text", nullable: true),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    vsme_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    iso_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gri_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ecovadis_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.id);
                    table.UniqueConstraint("AK_questions_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sector_weights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sector_weights", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "diagnostics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    global_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostics", x => x.id);
                    table.ForeignKey(
                        name: "FK_diagnostics_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_login = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action_text = table.Column<string>(type: "text", nullable: false),
                    detail_text = table.Column<string>(type: "text", nullable: true),
                    impact_points = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    effort_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger_question_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger_max_value = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendations_questions_trigger_question_code",
                        column: x => x.trigger_question_code,
                        principalTable: "questions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "domain_scores",
                columns: table => new
                {
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    sector_weight = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_scores", x => new { x.diagnostic_id, x.domain });
                    table.ForeignKey(
                        name: "FK_domain_scores_diagnostics_diagnostic_id",
                        column: x => x.diagnostic_id,
                        principalTable: "diagnostics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_responses_diagnostics_diagnostic_id",
                        column: x => x.diagnostic_id,
                        principalTable: "diagnostics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_responses_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_reports_diagnostics_diagnostic_id",
                        column: x => x.diagnostic_id,
                        principalTable: "diagnostics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reports_users_generated_by_user_id",
                        column: x => x.generated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_recommendations",
                columns: table => new
                {
                    diagnostic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priority_rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_recommendations", x => new { x.diagnostic_id, x.recommendation_id });
                    table.ForeignKey(
                        name: "FK_diagnostic_recommendations_diagnostics_diagnostic_id",
                        column: x => x.diagnostic_id,
                        principalTable: "diagnostics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_diagnostic_recommendations_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "questions",
                columns: new[] { "id", "code", "display_order", "domain", "ecovadis_ref", "gri_ref", "help_text", "is_active", "iso_ref", "text", "vsme_ref", "weight" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0004-000000000001"), "ENV-01", 1, "Environmental", null, "305-1", "Le scope 1 couvre les émissions directes (véhicules, chaudières), le scope 2 les émissions liées à l'électricité achetée.", true, "6.5.5", "Mesurez-vous et suivez-vous vos émissions de gaz à effet de serre (scope 1 et 2) ?", "B3", 3.00m },
                    { new Guid("00000000-0000-0000-0004-000000000002"), "ENV-02", 2, "Environmental", null, null, "Il peut s'agir d'objectifs chiffrés, d'un suivi des consommations ou d'investissements en efficacité énergétique.", true, null, "Avez-vous mis en place un plan de réduction de votre consommation d'énergie ?", "B4", 2.00m },
                    { new Guid("00000000-0000-0000-0004-000000000003"), "ENV-03", 3, "Environmental", null, "306-2", "Valoriser signifie recycler, réemployer ou faire traiter les déchets par une filière dédiée plutôt que les envoyer en décharge.", true, null, "Triez-vous et valorisez-vous vos déchets d'activité ?", "B7", 1.00m }
                });

            migrationBuilder.InsertData(
                table: "sector_weights",
                columns: new[] { "id", "domain", "is_default", "sector_code", "weight" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "Environmental", true, null, 0.200m },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "Social", true, null, 0.200m },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "Ethics", true, null, 0.200m },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "Procurement", true, null, 0.200m },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "Governance", true, null, 0.200m }
                });

            migrationBuilder.InsertData(
                table: "sector_weights",
                columns: new[] { "id", "domain", "sector_code", "weight" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), "Environmental", "4941A", 0.400m },
                    { new Guid("00000000-0000-0000-0002-000000000002"), "Social", "4941A", 0.200m },
                    { new Guid("00000000-0000-0000-0002-000000000003"), "Ethics", "4941A", 0.150m },
                    { new Guid("00000000-0000-0000-0002-000000000004"), "Procurement", "4941A", 0.150m },
                    { new Guid("00000000-0000-0000-0002-000000000005"), "Governance", "4941A", 0.100m },
                    { new Guid("00000000-0000-0000-0003-000000000001"), "Environmental", "6202A", 0.100m },
                    { new Guid("00000000-0000-0000-0003-000000000002"), "Social", "6202A", 0.300m },
                    { new Guid("00000000-0000-0000-0003-000000000003"), "Ethics", "6202A", 0.250m },
                    { new Guid("00000000-0000-0000-0003-000000000004"), "Procurement", "6202A", 0.150m },
                    { new Guid("00000000-0000-0000-0003-000000000005"), "Governance", "6202A", 0.200m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_companies_siret",
                table: "companies",
                column: "siret",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_recommendations_recommendation_id",
                table: "diagnostic_recommendations",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagnostics_company_id",
                table: "diagnostics",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_code",
                table: "recommendations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_trigger_question_code",
                table: "recommendations",
                column: "trigger_question_code");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_diagnostic_id",
                table: "reports",
                column: "diagnostic_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_generated_by_user_id",
                table: "reports",
                column: "generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_responses_diagnostic_id_question_id",
                table: "responses",
                columns: new[] { "diagnostic_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_responses_question_id",
                table: "responses",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_sector_weights_default_domain",
                table: "sector_weights",
                column: "domain",
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "IX_sector_weights_sector_code_domain",
                table: "sector_weights",
                columns: new[] { "sector_code", "domain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_company_id",
                table: "users",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnostic_recommendations");

            migrationBuilder.DropTable(
                name: "domain_scores");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "responses");

            migrationBuilder.DropTable(
                name: "sector_weights");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "diagnostics");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
