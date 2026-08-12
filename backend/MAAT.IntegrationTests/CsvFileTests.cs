using System.Reflection;
using MAAT.Infrastructure.Seed;

namespace MAAT.IntegrationTests;

// docs/specs/modele-donnees.md, section « Seed des données de référence » : robustesse de
// CsvFile face aux exports réels de tableur. Tests unitaires purs (aucune base de données)
// contre des fichiers d'exemple sous SeedSamples/, un par cas — CsvFile est internal,
// visible ici via InternalsVisibleTo (MAAT.Infrastructure.csproj).
//
// bom.csv et crlf.csv sont fabriqués octet par octet (voir leur commit) plutôt qu'écrits
// par un éditeur de texte ordinaire : un BOM ou un CRLF est justement le genre de détail
// qu'un éditeur ou une configuration Git (core.autocrlf) peut silencieusement renormaliser
// à l'insertion ou à l'extraction, ce qui invaliderait le cas testé sans que ça se voie.
public class CsvFileTests
{
    private static Stream SampleStream(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"MAAT.IntegrationTests.SeedSamples.{fileName}";
        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Fichier d'exemple introuvable : {resourceName}");
    }

    [Fact]
    public void Detecte_et_retire_le_BOM_UTF8_en_tete_de_fichier()
    {
        using var stream = SampleStream("bom.csv");

        var rows = CsvFile.ReadRows(stream, "bom.csv").Rows;

        var row = Assert.Single(rows);
        Assert.Equal("ENV-EX01", row.Fields["code"]);
        Assert.Equal("Environmental", row.Fields["domain"]);
    }

    [Fact]
    public void Accepte_les_fins_de_ligne_CRLF_comme_LF()
    {
        using var stream = SampleStream("crlf.csv");

        var rows = CsvFile.ReadRows(stream, "crlf.csv").Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("ENV-EX01", rows[0].Fields["code"]);
        Assert.Equal("ENV-EX02", rows[1].Fields["code"]);
        Assert.Equal("Première question.", rows[0].Fields["text"]);
    }

    [Fact]
    public void Gere_un_retour_a_la_ligne_a_l_interieur_d_un_champ_entre_guillemets()
    {
        using var stream = SampleStream("embedded-newline.csv");

        var rows = CsvFile.ReadRows(stream, "embedded-newline.csv").Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal(
            "Première ligne de la question.\nSeconde ligne, toujours dans le même champ.",
            rows[0].Fields["text"]);
        Assert.Equal("ENV-EX02", rows[1].Fields["code"]);
        Assert.Equal("Question simple sur une seule ligne.", rows[1].Fields["text"]);
    }

    [Fact]
    public void Echoue_avec_un_message_explicite_si_le_fichier_est_separe_par_des_points_virgules()
    {
        using var stream = SampleStream("semicolon-separated.csv");

        var ex = Assert.Throws<InvalidOperationException>(() => CsvFile.ReadRows(stream, "semicolon-separated.csv"));

        Assert.Contains(
            "fichier exporté avec le séparateur point-virgule — réenregistrer en CSV UTF-8 séparé par des virgules",
            ex.Message);
        Assert.Contains("semicolon-separated.csv", ex.Message);
    }

    [Fact]
    public void Ignore_les_lignes_de_commentaire_en_tete_de_fichier()
    {
        using var stream = SampleStream("leading-comment.csv");

        var rows = CsvFile.ReadRows(stream, "leading-comment.csv").Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("ENV-EX01", rows[0].Fields["code"]);
        Assert.Equal("ENV-EX02", rows[1].Fields["code"]);
    }

    [Fact]
    public void Conserve_la_numerotation_des_lignes_physiques_malgre_un_commentaire_de_tete()
    {
        using var stream = SampleStream("leading-comment-wrong-column-count.csv");

        var ex = Assert.Throws<InvalidOperationException>(
            () => CsvFile.ReadRows(stream, "leading-comment-wrong-column-count.csv"));

        // Ligne 1 = commentaire, 2 = en-tête, 3 = ENV-EX01 (valide), 4 = ENV-EX02 (fautive) :
        // le numéro rapporté doit rester celui du fichier sur disque, pas celui du contenu
        // une fois le commentaire retiré.
        Assert.Contains("ligne 4", ex.Message);
    }

    [Fact]
    public void Signale_le_numero_de_ligne_en_cas_de_mauvais_nombre_de_colonnes()
    {
        using var stream = SampleStream("wrong-column-count.csv");

        var ex = Assert.Throws<InvalidOperationException>(() => CsvFile.ReadRows(stream, "wrong-column-count.csv"));

        // La ligne 3 (ENV-EX02,Environmental) n'a que deux colonnes sur les trois attendues.
        Assert.Contains("wrong-column-count.csv", ex.Message);
        Assert.Contains("ligne 3", ex.Message);
        Assert.Contains("3 colonne(s) attendue(s)", ex.Message);
        Assert.Contains("2 trouvée(s)", ex.Message);
    }
}
