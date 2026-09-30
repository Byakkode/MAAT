using MAAT.Application.Interfaces;

namespace MAAT.IntegrationTests.TestDoubles;

// ADR 0013 : aucun test ne dépend du service de l'IGN. Une adresse qui contient « introuvable »
// n'est pas localisée, comme une adresse inconnue du service ou un service indisponible ; les
// autres le sont, à une position fixe. Calls compte les appels, pour vérifier qu'un site dont
// l'adresse ne change pas n'est pas géocodé de nouveau.
public sealed class FakeGeocoder : IGeocoder
{
    private int _calls;

    public int Calls => _calls;

    public Task<GeocodedAddress?> GeocodeAsync(string address, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(address.Contains("introuvable", StringComparison.OrdinalIgnoreCase)
            ? null
            : new GeocodedAddress(48.8686, 2.3308, $"{address} (localisé)"));
    }
}
