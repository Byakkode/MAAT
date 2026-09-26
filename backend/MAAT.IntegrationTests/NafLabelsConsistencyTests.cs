using System.Text.RegularExpressions;
using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// Pdf/naf-labels.tsv est une copie de frontend/src/constants/nafCodes.ts (le libellé choisi à
// l'inscription). Même principe que DomainColorConsistencyTests : ce test lit les deux sources
// réelles plutôt qu'une troisième copie, pour qu'un code ajouté ou un libellé corrigé d'un seul
// côté vire au rouge — l'entreprise ne doit pas lire un intitulé d'activité à l'écran et un
// autre sur son rapport.
public class NafLabelsConsistencyTests
{
    [Fact]
    public void Les_libelles_du_rapport_sont_ceux_du_formulaire_d_inscription()
    {
        var frontend = ReadFrontendLabels();

        Assert.NotEmpty(frontend);
        Assert.Equal(
            frontend.OrderBy(p => p.Key, StringComparer.Ordinal),
            NafLabels.All.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Code_connu_et_code_absent()
    {
        Assert.Equal("Programmation informatique", NafLabels.For("6201Z"));
        Assert.Equal("Programmation informatique", NafLabels.For(" 6201z "));
        Assert.Null(NafLabels.For("9999Z"));
    }

    private static Dictionary<string, string> ReadFrontendLabels()
    {
        var path = Path.Combine(FindRepoRoot(), "frontend", "src", "constants", "nafCodes.ts");
        var source = File.ReadAllText(path);

        return Regex.Matches(source, @"\{ code: '([0-9A-Z]+)', label: '((?:[^'\\]|\\.)*)', section: '[A-Z]' \}")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Replace("\\'", "'"), StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !(Directory.Exists(Path.Combine(dir.FullName, "frontend")) && Directory.Exists(Path.Combine(dir.FullName, "backend"))))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Racine du dépôt introuvable en remontant depuis {AppContext.BaseDirectory}.");
        return dir!.FullName;
    }
}
