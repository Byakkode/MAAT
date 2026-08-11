using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class DomainScore
{
    public Guid DiagnosticId { get; private set; }
    public RseDomain Domain { get; private set; }
    public decimal Score { get; set; }
    public decimal SectorWeight { get; set; }

    // docs/specs/rapport-pdf.md, section 4 : le tableau de détail du rapport PDF doit
    // exposer le numérateur et le dénominateur du calcul (Σ(r×w) et Σ(w×5), voir
    // ScoringService.DomainScoreDetail) pour que le lecteur puisse refaire l'opération.
    // Persistés ici plutôt que recalculés à la génération du rapport : les recalculer
    // depuis Response/Question.Weight au moment de la génération romprait le déterminisme
    // de la section 3 de la même façon qu'une relecture de SectorWeight le romprait — rien
    // n'interdit qu'un Question.Weight soit modifié après coup (voir modele-donnees.md).
    public decimal Numerator { get; set; }
    public decimal Denominator { get; set; }

    private DomainScore()
    {
    }

    public DomainScore(Guid diagnosticId, RseDomain domain, decimal score, decimal sectorWeight, decimal numerator, decimal denominator)
    {
        if (score < 0 || score > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Le score d'un domaine doit être compris entre 0 et 100.");
        }

        DiagnosticId = diagnosticId;
        Domain = domain;
        Score = score;
        SectorWeight = sectorWeight;
        Numerator = numerator;
        Denominator = denominator;
    }
}
