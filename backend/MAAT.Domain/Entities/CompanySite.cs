using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

// Site détenu, loué ou géré par l'entreprise (docs/specs/norme-volontaire.md) : sa
// géolocalisation sert l'information B1 (§27 e vii), sa situation vis-à-vis d'une zone
// sensible pour la biodiversité l'information B5 (§35). Rattaché à l'entreprise et non à un
// exercice : un entrepôt ne déménage pas au 1er janvier, et le rapport présente les sites
// tels qu'enregistrés à la date de génération (rapport-pdf.md, section 3).
public class CompanySite
{
    public const int NameMaxLength = 120;
    public const int AddressMaxLength = 300;
    public const int SensitiveAreaNameMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public SiteTenure Tenure { get; private set; }

    // Coordonnées obtenues par géocodage de Address (ADR 0013). Null : adresse introuvable ou
    // service indisponible au moment de l'enregistrement.
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? GeocodedLabel { get; private set; }

    // B5 : null = pas encore répondu, distinct de « non ».
    public bool? InOrNearSensitiveArea { get; private set; }
    public string? SensitiveAreaName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CompanySite() { }

    public CompanySite(Guid companyId, CompanySiteDetails details, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        CreatedAt = now;
        Apply(details, now);
    }

    public bool IsGeolocated => Latitude is not null && Longitude is not null;

    // Vrai quand l'adresse a changé : l'appelant doit alors géocoder de nouveau. Les anciennes
    // coordonnées sont effacées dès maintenant, pour qu'un site ne garde jamais la position
    // d'une adresse qu'il n'a plus.
    public bool Update(CompanySiteDetails details, DateTimeOffset now)
    {
        var addressChanged = !string.Equals(Address, details.Address.Trim(), StringComparison.Ordinal);
        Apply(details, now);
        if (addressChanged)
        {
            ClearLocation();
        }

        return addressChanged;
    }

    public void Locate(double latitude, double longitude, string label)
    {
        Latitude = latitude;
        Longitude = longitude;
        GeocodedLabel = label.Length > AddressMaxLength ? label[..AddressMaxLength] : label;
    }

    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        GeocodedLabel = null;
    }

    private void Apply(CompanySiteDetails details, DateTimeOffset now)
    {
        Name = details.Name.Trim();
        Address = details.Address.Trim();
        Tenure = details.Tenure;
        InOrNearSensitiveArea = details.InOrNearSensitiveArea;
        SensitiveAreaName = details.InOrNearSensitiveArea == true && !string.IsNullOrWhiteSpace(details.SensitiveAreaName)
            ? details.SensitiveAreaName.Trim()
            : null;
        UpdatedAt = now;
    }
}

public sealed record CompanySiteDetails(
    string Name,
    string Address,
    SiteTenure Tenure,
    bool? InOrNearSensitiveArea,
    string? SensitiveAreaName);
