using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// Table de référence, comme IQuestionRepository : pas de filtrage par company_id.
public interface ISectorWeightRepository
{
    // modele-donnees.md, logique de repli : les lignes du secteur si sector_code en a au
    // moins une, sinon (aucune ligne trouvée pour ce secteur, pas domaine par domaine) les
    // lignes par défaut. Ne lève jamais d'exception ici — une pondération manquante pour un
    // domaine précis est détectée par ScoringService, pas ce dépôt (docs/specs/scoring.md).
    Task<IReadOnlyDictionary<RseDomain, decimal>> GetForSectorOrDefaultAsync(string sectorCode, CancellationToken ct);
}
