using MAAT.Infrastructure.Pdf;

namespace MAAT.IntegrationTests;

// Pdf/Assets/maat-logo-blanc.png est une copie de frontend/src/assets/maat-logo-blanc.png.
// Même principe que NafLabelsConsistencyTests : ce test lit les deux fichiers réels, pour qu'un
// logo mis à jour d'un seul côté vire au rouge — le rapport et l'application doivent porter la
// même marque.
public class ReportAssetsConsistencyTests
{
    [Fact]
    public void Le_logo_du_rapport_est_celui_de_l_application()
    {
        var frontend = File.ReadAllBytes(Path.Combine(FindRepoRoot(), "frontend", "src", "assets", "maat-logo-blanc.png"));

        Assert.NotEmpty(frontend);
        Assert.Equal(frontend, ReportAssets.MaatLogoWhitePng);
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
