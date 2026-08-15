using System.Text;
using MAAT.Infrastructure.Seed;

namespace MAAT.IntegrationTests;

// docs/specs/modele-donnees.md, section « Seed des données de référence » : la commande
// "seed --validate", exercée ici directement contre ReferenceDataValidator — aucune base
// de données, aucun processus. Chaque cas fabrique un contenu CSV minimal en mémoire via
// la surcharge internal Validate(Func<string, IReadOnlyList<CsvRow>>) plutôt que les
// fichiers de seed réels, pour isoler précisément la règle testée.
public class ReferenceDataValidatorTests
{
    private const string ValidQuestions = """
        code,domain,text,help_text,weight,display_order,vsme_ref,iso_ref,gri_ref,ecovadis_ref,is_active
        Q1,Environmental,Question un,,1.00,1,,,,,true
        Q2,Social,Question deux,,1.00,2,,,,,true
        """;

    // R1 déclenche sur Q1 (poids 1.00, seul du domaine Environmental) : gain maximal
    // théorique = (5-2)×1/5×100 = 60. impact_points = 1.00 reste largement en dessous.
    private const string ValidRecommendations = """
        code,domain,action_text,detail_text,impact_points,effort_level,trigger_question_code,trigger_max_value,is_active
        R1,Environmental,Action un,,1.00,Low,Q1,2,true
        """;

    private const string ValidSectorWeights = """
        sector_code,domain,weight
        ,Environmental,0.2
        ,Social,0.2
        ,Ethics,0.2
        ,Procurement,0.2
        ,Governance,0.2
        """;

    private static ReferenceDataValidationResult Validate(
        string questionsCsv, string recommendationsCsv, string sectorWeightsCsv)
    {
        var files = new Dictionary<string, string>
        {
            ["questions.csv"] = questionsCsv,
            ["recommendations.csv"] = recommendationsCsv,
            ["sector-weights.csv"] = sectorWeightsCsv,
        };

        CsvFileContent ReadCsv(string fileName)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(files[fileName]));
            return CsvFile.ReadRows(stream, fileName);
        }

        return ReferenceDataValidator.Validate(ReadCsv);
    }

    [Fact]
    public void Les_fichiers_de_seed_reels_sont_valides_sans_avertissement()
    {
        var result = ReferenceDataValidator.Validate();

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Un_contenu_minimal_valide_ne_produit_ni_erreur_ni_avertissement()
    {
        var result = Validate(ValidQuestions, ValidRecommendations, ValidSectorWeights);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Signale_un_code_en_doublon_dans_questions_csv()
    {
        const string questionsWithDuplicate = """
            code,domain,text,help_text,weight,display_order,vsme_ref,iso_ref,gri_ref,ecovadis_ref,is_active
            Q1,Environmental,Question un,,1.00,1,,,,,true
            Q1,Social,Question deux (même code),,1.00,2,,,,,true
            """;

        var result = Validate(questionsWithDuplicate, ValidRecommendations, ValidSectorWeights);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("questions.csv") && e.Contains("doublon") && e.Contains("Q1"));
    }

    [Fact]
    public void Signale_une_valeur_de_domaine_invalide()
    {
        const string questionsWithBadDomain = """
            code,domain,text,help_text,weight,display_order,vsme_ref,iso_ref,gri_ref,ecovadis_ref,is_active
            Q1,Environnement,Question un,,1.00,1,,,,,true
            """;

        var result = Validate(questionsWithBadDomain, ValidRecommendations, ValidSectorWeights);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("questions.csv") && e.Contains("ligne 2") && e.Contains("Environnement"));
    }

    [Fact]
    public void Signale_une_somme_de_ponderations_sectorielles_differente_de_1()
    {
        const string sectorWeightsWrongSum = """
            sector_code,domain,weight
            ,Environmental,0.3
            ,Social,0.2
            ,Ethics,0.2
            ,Procurement,0.2
            ,Governance,0.2
            """;

        var result = Validate(ValidQuestions, ValidRecommendations, sectorWeightsWrongSum);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sector-weights.csv") && e.Contains("somme des poids") && e.Contains("1.1"));
    }

    [Fact]
    public void Signale_un_domaine_manquant_pour_un_secteur()
    {
        const string sectorWeightsMissingDomain = """
            sector_code,domain,weight
            ,Environmental,0.25
            ,Social,0.25
            ,Ethics,0.25
            ,Procurement,0.25
            """;

        var result = Validate(ValidQuestions, ValidRecommendations, sectorWeightsMissingDomain);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sector-weights.csv") && e.Contains("manquant") && e.Contains("Governance"));
    }

    [Fact]
    public void Signale_une_colonne_facultative_absente_de_l_en_tete()
    {
        // help_text absent de l'en-tête : sans le contrôle d'en-tête, OrNull("help_text")
        // renverrait silencieusement null pour chaque ligne, indiscernable d'une colonne
        // présente mais vide (docs/specs/modele-donnees.md, « Fichiers et colonnes »).
        const string questionsMissingOptionalColumn = """
            code,domain,text,weight,display_order,vsme_ref,iso_ref,gri_ref,ecovadis_ref,is_active
            Q1,Environmental,Question un,1.00,1,,,,,true
            """;

        var result = Validate(questionsMissingOptionalColumn, ValidRecommendations, ValidSectorWeights);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Contains("questions.csv") && e.Contains("manquante") && e.Contains("help_text"));
    }

    [Fact]
    public void Signale_une_colonne_inattendue_dans_l_en_tete()
    {
        // Renommage/faute de frappe imaginaire : "poids" au lieu de "weight". La colonne
        // attendue est donc aussi signalée manquante (les deux erreurs sont complémentaires,
        // pas redondantes : l'une dit ce qui a disparu, l'autre ce qui est apparu à la place).
        const string sectorWeightsRenamedColumn = """
            sector_code,domain,poids
            ,Environmental,0.2
            ,Social,0.2
            ,Ethics,0.2
            ,Procurement,0.2
            ,Governance,0.2
            """;

        var result = Validate(ValidQuestions, ValidRecommendations, sectorWeightsRenamedColumn);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Contains("sector-weights.csv") && e.Contains("inattendue") && e.Contains("poids"));
        Assert.Contains(
            result.Errors,
            e => e.Contains("sector-weights.csv") && e.Contains("manquante") && e.Contains("weight"));
    }

    [Fact]
    public void Signale_en_avertissement_une_recommandation_mal_calibree_sans_faire_echouer_la_validation()
    {
        // Même R1, impact_points porté à 99 — largement au-dessus du gain maximal
        // théorique de 60 (voir ValidRecommendations).
        const string miscalibratedRecommendations = """
            code,domain,action_text,detail_text,impact_points,effort_level,trigger_question_code,trigger_max_value,is_active
            R1,Environmental,Action un,,99.00,Low,Q1,2,true
            """;

        var result = Validate(ValidQuestions, miscalibratedRecommendations, ValidSectorWeights);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        Assert.Contains(result.Warnings, w => w.Contains("R1") && w.Contains("impact_points"));
    }
}
