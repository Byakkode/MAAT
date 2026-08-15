using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// UsedDefaultFallback distingue le repli du cas nominal à la source, avant toute
// renormalisation par ScoringService (docs/specs/scoring.md, cas 7) : celle-ci ramène le
// coefficient effectif à 1.00 quand un seul domaine est actif, que la pondération d'origine
// soit spécifique ou par défaut — les deux situations deviennent alors indiscernables une
// fois DomainScore.SectorWeight persisté. DiagnosticService persiste ce booléen tel quel sur
// Diagnostic (questionnaire.md, section 6, cas 13) : c'est la seule source fiable de
// l'indicateur affiché sur la page de garde du rapport (rapport-pdf.md, section 4).
public sealed record SectorWeightLookup(IReadOnlyDictionary<RseDomain, decimal> Weights, bool UsedDefaultFallback);

// Table de référence, comme IQuestionRepository : pas de filtrage par company_id.
public interface ISectorWeightRepository
{
    // modele-donnees.md, logique de repli : les lignes du secteur si sector_code en a au
    // moins une, sinon (aucune ligne trouvée pour ce secteur, pas domaine par domaine) les
    // lignes par défaut. Ne lève jamais d'exception ici — une pondération manquante pour un
    // domaine précis est détectée par ScoringService, pas ce dépôt (docs/specs/scoring.md).
    Task<SectorWeightLookup> GetForSectorOrDefaultAsync(string sectorCode, CancellationToken ct);
}
