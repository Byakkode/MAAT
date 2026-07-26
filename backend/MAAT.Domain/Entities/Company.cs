using MAAT.Domain.Enums;
using MAAT.Domain.ValueObjects;

namespace MAAT.Domain.Entities;

public class Company
{
    public Guid Id { get; private set; }
    public string? Siret { get; set; }
    public string Name { get; set; } = default!;
    public string SectorCode { get; set; } = default!;
    public CompanySizeRange SizeRange { get; set; }
    public string Region { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Company()
    {
    }

    public Company(string name, string sectorCode, CompanySizeRange sizeRange, string region, string? siret = null)
    {
        if (siret is not null)
        {
            SiretValidator.Validate(siret);
        }

        Id = Guid.NewGuid();
        Name = name;
        SectorCode = sectorCode;
        SizeRange = sizeRange;
        Region = region;
        Siret = siret;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
