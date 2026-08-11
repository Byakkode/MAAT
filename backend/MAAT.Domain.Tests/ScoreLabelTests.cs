using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/dashboard.md, section 2, et docs/specs/rapport-pdf.md, cas 14 : les cinq
// tranches doivent produire exactement les libellés de frontend/src/constants/scoreLabels.ts.
public class ScoreLabelTests
{
    [Theory]
    [InlineData(0, "Démarche à initier")]
    [InlineData(24, "Démarche à initier")]
    [InlineData(25, "Premiers pas engagés")]
    [InlineData(49, "Premiers pas engagés")]
    [InlineData(50, "Démarche structurée")]
    [InlineData(69, "Démarche structurée")]
    [InlineData(70, "Démarche avancée")]
    [InlineData(84, "Démarche avancée")]
    [InlineData(85, "Démarche exemplaire")]
    [InlineData(100, "Démarche exemplaire")]
    public void Cas14_LibelleQualitatif_ExactementCeluiDesCinqTranches(int roundedScore, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ScoreLabel.For(roundedScore));
    }
}
