using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Tests;

// L'échéance est un jour du calendrier. L'écran envoie « 2026-11-15 » ; interprété à l'heure
// d'un serveur réglé sur Paris, il arrive en 2026-11-15T00:00+01:00, que PostgreSQL refuse
// (timestamptz n'accepte qu'un décalage nul) — et une conversion en UTC le ramènerait au 14.
public class ActionItemProgressTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-5)]
    [InlineData(0)]
    public void Echeance_conservee_au_jour_choisi_a_minuit_UTC(int offsetHours)
    {
        var progress = ActionItemProgress.Create(Guid.NewGuid(), "ENV-01");

        progress.Update(ActionItemStatus.Planned, null, new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.FromHours(offsetHours)), null);

        Assert.Equal(new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), progress.DueDate);
    }

    [Fact]
    public void Echeance_absente_reste_absente()
    {
        var progress = ActionItemProgress.Create(Guid.NewGuid(), "ENV-01");

        progress.Update(ActionItemStatus.Planned, null, null, null);

        Assert.Null(progress.DueDate);
    }
}
