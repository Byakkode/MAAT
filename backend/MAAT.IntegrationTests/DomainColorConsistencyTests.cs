using System.Text.RegularExpressions;
using MAAT.Domain.Enums;
using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// docs/specs/charte-maat-v2.md, section 6 : "Le rapport PDF lit les mêmes valeurs [que
// frontend/src/index.css]. Une couleur de domaine qui diverge entre l'écran et le document
// ferait douter du reste." Ce test lit les deux sources réelles — le dictionnaire
// RadarChartRenderer.DomainColors effectivement utilisé par le générateur PDF, et le fichier
// frontend/src/index.css effectivement chargé par Vite — plutôt que de recopier les cinq
// valeurs hexadécimales une troisième fois ici : une constante recopiée resterait verte si
// une seule des deux sources dérivait, ce qui ne vérifierait rien.
public class DomainColorConsistencyTests
{
    private static readonly IReadOnlyDictionary<RseDomain, string> CssTokenByDomain = new Dictionary<RseDomain, string>
    {
        [RseDomain.Environmental] = "--color-chart-environnement",
        [RseDomain.Social] = "--color-chart-social",
        [RseDomain.Ethics] = "--color-chart-ethique",
        [RseDomain.Procurement] = "--color-chart-achats",
        [RseDomain.Governance] = "--color-chart-gouvernance",
    };

    [Theory]
    [InlineData(RseDomain.Environmental)]
    [InlineData(RseDomain.Social)]
    [InlineData(RseDomain.Ethics)]
    [InlineData(RseDomain.Procurement)]
    [InlineData(RseDomain.Governance)]
    public void Couleur_du_domaine_identique_entre_le_PDF_et_index_css(RseDomain domain)
    {
        var pdfColor = RadarChartRenderer.DomainColors[domain];
        var cssColor = ReadCssCustomProperty(CssTokenByDomain[domain]);

        Assert.Equal(cssColor, (pdfColor.Red, pdfColor.Green, pdfColor.Blue));
    }

    private static (byte R, byte G, byte B) ReadCssCustomProperty(string tokenName)
    {
        var cssPath = Path.Combine(FindRepoRoot(), "frontend", "src", "index.css");
        var css = File.ReadAllText(cssPath);

        var match = Regex.Match(css, $@"{Regex.Escape(tokenName)}:\s*#([0-9a-fA-F]{{6}});");
        Assert.True(match.Success, $"Token CSS '{tokenName}' introuvable dans {cssPath}.");

        var hex = match.Groups[1].Value;
        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
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
