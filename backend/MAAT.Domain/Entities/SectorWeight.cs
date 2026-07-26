using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class SectorWeight
{
    public Guid Id { get; private set; }
    public string? SectorCode { get; private set; }
    public bool IsDefault { get; private set; }
    public RseDomain Domain { get; private set; }
    public decimal Weight { get; set; }

    private SectorWeight()
    {
    }

    private SectorWeight(string? sectorCode, bool isDefault, RseDomain domain, decimal weight)
    {
        if (weight < 0 || weight > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Le poids sectoriel d'un domaine doit être compris entre 0 et 1.");
        }

        Id = Guid.NewGuid();
        SectorCode = sectorCode;
        IsDefault = isDefault;
        Domain = domain;
        Weight = weight;
    }

    public static SectorWeight ForSector(string sectorCode, RseDomain domain, decimal weight) =>
        new(sectorCode, isDefault: false, domain, weight);

    public static SectorWeight Default(RseDomain domain, decimal weight) =>
        new(sectorCode: null, isDefault: true, domain, weight);
}
