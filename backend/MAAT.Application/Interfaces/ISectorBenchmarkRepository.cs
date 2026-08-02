namespace MAAT.Application.Interfaces;

// docs/specs/dashboard.md, section 5 : seule exception délibérée au patron « dépôt scopé par
// company_id courant » (voir IDiagnosticRepository) — le benchmark sectoriel compare
// nécessairement plusieurs entreprises. Le type de retour (une liste de decimal et rien
// d'autre) rend structurellement impossible toute fuite d'identifiant, de nom ou de
// diagnostic individuel d'une autre entreprise (cas 11) : même l'appelant ne peut pas exposer
// ce que ce dépôt ne retourne jamais.
public interface ISectorBenchmarkRepository
{
    // Un score par entreprise du secteur donné ayant au moins un diagnostic Completed — le
    // plus récent de chacune, jamais une moyenne sur l'ensemble de ses diagnostics, pour ne
    // jamais pondérer une entreprise plus qu'une autre selon le nombre de diagnostics qu'elle
    // a réalisés (cas 10).
    Task<IReadOnlyList<decimal>> FindLatestCompletedScoresBySectorAsync(string sectorCode, CancellationToken ct);
}
