namespace MAAT.Application.Interfaces;

// ADR 0013 : géocodage d'une adresse de site. Null quand l'adresse est introuvable ou que le
// service ne répond pas : l'implémentation ne lève jamais, l'enregistrement du site ne doit
// pas dépendre d'un service externe.
public interface IGeocoder
{
    Task<GeocodedAddress?> GeocodeAsync(string address, CancellationToken ct);
}

public sealed record GeocodedAddress(double Latitude, double Longitude, string Label);
