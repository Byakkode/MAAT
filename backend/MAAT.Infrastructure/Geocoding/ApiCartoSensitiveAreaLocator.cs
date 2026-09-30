using System.Globalization;
using System.Text;
using System.Text.Json;
using MAAT.Application.Interfaces;
using MAAT.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAAT.Infrastructure.Geocoding;

public sealed class SensitiveAreaOptions
{
    public const string Section = "SensitiveAreas";

    // ADR 0014 : module Nature de l'API Carto de l'IGN (données INPN), sans clé.
    public string BaseUrl { get; init; } = "https://apicarto.ign.fr/api/nature/";

    // Approximation de « chevauche ou jouxte » (norme volontaire, annexe A, « Near ») : on ne
    // connaît que l'adresse du site, pas son emprise.
    public double RadiusMeters { get; init; } = 500;

    public int TimeoutSeconds { get; init; } = 8;
}

// ADR 0014. Interroge en parallèle les couches retenues autour d'un cercle de RadiusMeters.
// Ne lève jamais : une couche en erreur rend toute la recherche nulle (voir
// ISensitiveAreaLocator), et le site reste « non vérifié ».
public sealed class ApiCartoSensitiveAreaLocator(
    HttpClient httpClient,
    IOptions<SensitiveAreaOptions> options,
    ILogger<ApiCartoSensitiveAreaLocator> logger) : ISensitiveAreaLocator
{
    // Chemin de la couche, catégorie, propriété qui porte le nom de la zone (elle diffère selon
    // les couches, voir la définition OpenAPI du module Nature).
    internal static readonly (string Path, SensitiveAreaKind Kind, string NameProperty)[] Layers =
    [
        ("natura-habitat", SensitiveAreaKind.NaturaHabitats, "sitename"),
        ("natura-oiseaux", SensitiveAreaKind.NaturaBirds, "sitename"),
        ("rnn", SensitiveAreaKind.NatureReserve, "nom"),
        ("rnc", SensitiveAreaKind.NatureReserve, "nom"),
        ("pn", SensitiveAreaKind.NationalPark, "nom"),
        ("rncf", SensitiveAreaKind.HuntingWildlifeReserve, "nom_site"),
        ("znieff1", SensitiveAreaKind.Znieff1, "nom"),
    ];

    public async Task<IReadOnlyList<SensitiveArea>?> FindNearAsync(double latitude, double longitude, CancellationToken ct)
    {
        var geometry = CirclePolygon(latitude, longitude, options.Value.RadiusMeters);
        var results = await Task.WhenAll(Layers.Select(layer => QueryAsync(layer, geometry, ct)));

        if (results.Any(r => r is null))
        {
            return null;
        }

        return [.. results.SelectMany(r => r!)];
    }

    private async Task<IReadOnlyList<SensitiveArea>?> QueryAsync(
        (string Path, SensitiveAreaKind Kind, string NameProperty) layer, string geometry, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.GetAsync($"{layer.Path}?geom={Uri.EscapeDataString(geometry)}&_limit=50", ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Zones sensibles : réponse {Status} pour la couche {Layer}.", (int)response.StatusCode, layer.Path);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseFeatures(document.RootElement, layer.Kind, layer.NameProperty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(ex, "Zones sensibles indisponibles pour la couche {Layer}.", layer.Path);
            return null;
        }
    }

    // Null si la réponse n'est pas une FeatureCollection : mieux vaut « non vérifié » qu'un
    // « aucune zone » tiré d'une réponse qu'on n'a pas comprise.
    internal static IReadOnlyList<SensitiveArea>? ParseFeatures(JsonElement root, SensitiveAreaKind kind, string nameProperty)
    {
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var areas = new List<SensitiveArea>();
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.TryGetProperty("properties", out var properties)
                && properties.TryGetProperty(nameProperty, out var name)
                && name.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                areas.Add(new SensitiveArea(name.GetString()!.Trim(), kind));
            }
        }

        return areas;
    }

    // Cercle approché par un polygone de 16 côtés, en GeoJSON WGS84 (longitude, latitude), le
    // format qu'attend le paramètre geom. Un degré de latitude vaut environ 111,32 km ; un degré
    // de longitude, autant multiplié par le cosinus de la latitude.
    internal static string CirclePolygon(double latitude, double longitude, double radiusMeters)
    {
        const int sides = 16;
        const double metersPerDegree = 111_320;
        var builder = new StringBuilder("{\"type\":\"Polygon\",\"coordinates\":[[");
        for (var i = 0; i <= sides; i++)
        {
            var angle = 2 * Math.PI * (i % sides) / sides;
            var lat = latitude + radiusMeters * Math.Cos(angle) / metersPerDegree;
            var lon = longitude + radiusMeters * Math.Sin(angle) / (metersPerDegree * Math.Cos(latitude * Math.PI / 180));
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(CultureInfo.InvariantCulture, $"[{Math.Round(lon, 6)},{Math.Round(lat, 6)}]");
        }

        return builder.Append("]]}").ToString();
    }
}
