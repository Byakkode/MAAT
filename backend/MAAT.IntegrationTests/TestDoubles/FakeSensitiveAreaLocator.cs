using MAAT.Application.Interfaces;
using MAAT.Domain.Services;

namespace MAAT.IntegrationTests.TestDoubles;

// ADR 0014 : aucun test ne dépend de l'API Carto. Result est ce que renvoie la prochaine
// recherche : une liste (vide ou non), ou null pour un service indisponible. Les tests d'une
// même collection s'exécutent l'un après l'autre : chacun pose la valeur qu'il attend.
public sealed class FakeSensitiveAreaLocator : ISensitiveAreaLocator
{
    public IReadOnlyList<SensitiveArea>? Result { get; set; } = [];

    public Task<IReadOnlyList<SensitiveArea>?> FindNearAsync(double latitude, double longitude, CancellationToken ct) =>
        Task.FromResult(Result);
}
