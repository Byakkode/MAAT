using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAAT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorWeightCoverageConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // modele-donnees.md : "pour un sector_code donné, la somme des weight sur les
            // 5 domaines vaut exactement 1" — jusqu'ici vérifié par un test applicatif sur
            // le seed, jamais par la base. Un CHECK ordinaire ne peut pas porter sur un
            // agrégat multi-lignes ; il faut un trigger. Il doit être différé en fin de
            // transaction (CONSTRAINT TRIGGER ... DEFERRABLE INITIALLY DEFERRED) : les 5
            // lignes d'un secteur s'insèrent une par une, la couverture n'est correcte
            // qu'une fois les 5 présentes.
            //
            // Ne couvre volontairement que sector_code non nul : les lignes par défaut
            // (is_default = true, sector_code nul) sont hors périmètre de cette contrainte,
            // conformément à la demande.
            //
            // Limite connue et acceptée : si sector_code d'une ligne existante était modifié
            // par UPDATE, seul le sector_code résultant (NEW) serait revérifié, pas l'ancien
            // (OLD) qui pourrait rester sous-couvert. Sans conséquence en pratique :
            // SectorWeight.SectorCode est private set côté C#, aucun chemin applicatif ne
            // permet une telle mise à jour.
            //
            // Zéro ligne pour un sector_code est un état valide (secteur non configuré,
            // équivalent à un code jamais seedé — le repli sur la pondération par défaut
            // s'applique) : seule une couverture partielle (1 à 4 domaines) est rejetée,
            // vérifié empiriquement — une suppression complète des 5 lignes d'un secteur
            // échouait initialement sans cette exception explicite.
            migrationBuilder.Sql("""
                CREATE FUNCTION check_sector_weight_coverage() RETURNS trigger
                LANGUAGE plpgsql
                AS $func$
                DECLARE
                    v_sector_code varchar(6);
                    v_domain_count integer;
                    v_weight_sum numeric;
                BEGIN
                    v_sector_code := COALESCE(NEW.sector_code, OLD.sector_code);

                    IF v_sector_code IS NULL THEN
                        RETURN NULL;
                    END IF;

                    SELECT COUNT(DISTINCT domain), COALESCE(SUM(weight), 0)
                    INTO v_domain_count, v_weight_sum
                    FROM sector_weights
                    WHERE sector_code = v_sector_code;

                    IF v_domain_count > 0 AND (v_domain_count <> 5 OR v_weight_sum <> 1) THEN
                        RAISE EXCEPTION 'sector_weights : le secteur % doit couvrir les 5 domaines avec une somme de poids egale a 1 (couverture actuelle : % domaine(s), somme %)',
                            v_sector_code, v_domain_count, v_weight_sum;
                    END IF;

                    RETURN NULL;
                END;
                $func$;
                """);

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER sector_weights_coverage_check
                    AFTER INSERT OR UPDATE OR DELETE ON sector_weights
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION check_sector_weight_coverage();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS sector_weights_coverage_check ON sector_weights;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS check_sector_weight_coverage();");
        }
    }
}
