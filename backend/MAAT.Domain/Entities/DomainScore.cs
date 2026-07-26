using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class DomainScore
{
    public Guid DiagnosticId { get; private set; }
    public RseDomain Domain { get; private set; }
    public decimal Score { get; set; }
    public decimal SectorWeight { get; set; }

    private DomainScore()
    {
    }

    public DomainScore(Guid diagnosticId, RseDomain domain, decimal score, decimal sectorWeight)
    {
        if (score < 0 || score > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Le score d'un domaine doit être compris entre 0 et 100.");
        }

        DiagnosticId = diagnosticId;
        Domain = domain;
        Score = score;
        SectorWeight = sectorWeight;
    }
}
