using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "recommendations",
                columns: new[] { "id", "action_text", "code", "detail_text", "domain", "effort_level", "impact_points", "is_active", "trigger_max_value", "trigger_question_code" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000001"), "Mettre en place un suivi mensuel de vos émissions de gaz à effet de serre (scope 1 et 2).", "REC-ENV-01", "Un tableur suffit pour démarrer : consommations de carburant, d'électricité et de gaz, converties en équivalent CO2 via les facteurs d'émission de la Base Empreinte de l'ADEME. Le dispositif Diag Décarbon'Action de l'ADEME peut financer un accompagnement.", "Environmental", "Medium", 15.00m, true, 2, "ENV-01" },
                    { new Guid("00000000-0000-0000-0005-000000000002"), "Fixer un objectif chiffré de réduction de votre consommation d'énergie sur trois ans.", "REC-ENV-02", "Un objectif simple (ex. -10 % sur trois ans) suffit pour démarrer une démarche de suivi. Bpifrance et les CCI proposent des diagnostics énergétiques subventionnés pour les PME.", "Environmental", "Low", 10.00m, true, 2, "ENV-02" },
                    { new Guid("00000000-0000-0000-0005-000000000003"), "Mettre en place le tri sélectif des déchets d'activité avec un prestataire agréé.", "REC-ENV-03", "Commencer par les flux les plus simples (papier, carton, emballages). France Num référence des prestataires locaux de collecte et de valorisation.", "Environmental", "Low", 5.00m, true, 2, "ENV-03" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "recommendations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"));

            migrationBuilder.DeleteData(
                table: "recommendations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"));

            migrationBuilder.DeleteData(
                table: "recommendations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000003"));
        }
    }
}
