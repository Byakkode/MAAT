using System.Globalization;
using System.Text.Json;
using MAAT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAAT.Infrastructure.Geocoding;

public sealed class GeocodingOptions
{
    public const string Section = "Geocoding";

    // ADR 0013 : service de géocodage de la Géoplateforme de l'IGN, sans clé. L'ancienne API
    // Adresse (api-adresse.data.gouv.fr) a été arrêtée fin janvier 2026.
    public string BaseUrl { get; init; } = "https://data.geopf.fr/geocodage/";

    public int TimeoutSeconds { get; init; } = 5;
}

// ADR 0013. Ne lève jamais : adresse introuvable, réponse inattendue, délai dépassé ou service
// indisponible donnent null, et le site est enregistré sans coordonnées. Seule l'adresse du
// site est transmise.
public sealed class GeoplateformeGeocoder(HttpClient httpClient, ILogger<GeoplateformeGeocoder> logger) : IGeocoder
{
    // Score de confiance du service (0 à 1). En dessous, le premier résultat ressemble trop peu
    // à l'adresse saisie : une commune voisine ou une rue homonyme. Mieux vaut « non localisé »,
    // que l'écran signale, qu'une position fausse dans un rapport transmis à des tiers.
    private const double MinimumScore = 0.5;

    public async Task<GeocodedAddress?> GeocodeAsync(string address, CancellationToken ct)
    {
        var query = $"search?q={Uri.EscapeDataString(address)}&index=address&limit=1";
        try
        {
            using var response = await httpClient.GetAsync(query, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Géocodage : réponse {Status} du service.", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return Parse(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(ex, "Géocodage indisponible, site enregistré sans coordonnées.");
            return null;
        }
    }

    // Réponse GeoJSON : features[0].geometry.coordinates = [longitude, latitude].
    internal static GeocodedAddress? Parse(JsonElement root)
    {
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array || features.GetArrayLength() == 0)
        {
            return null;
        }

        var feature = features[0];
        if (!feature.TryGetProperty("geometry", out var geometry)
            || !geometry.TryGetProperty("coordinates", out var coordinates)
            || coordinates.ValueKind != JsonValueKind.Array
            || coordinates.GetArrayLength() < 2
            || !feature.TryGetProperty("properties", out var properties))
        {
            return null;
        }

        if (properties.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number && score.GetDouble() < MinimumScore)
        {
            return null;
        }

        var label = properties.TryGetProperty("label", out var labelElement) && labelElement.ValueKind == JsonValueKind.String
            ? labelElement.GetString()
            : null;

        return new GeocodedAddress(
            Math.Round(coordinates[1].GetDouble(), 6),
            Math.Round(coordinates[0].GetDouble(), 6),
            string.IsNullOrWhiteSpace(label) ? string.Create(CultureInfo.InvariantCulture, $"{coordinates[1].GetDouble()}, {coordinates[0].GetDouble()}") : label);
    }
}
