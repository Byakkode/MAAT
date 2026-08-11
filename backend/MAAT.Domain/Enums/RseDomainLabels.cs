namespace MAAT.Domain.Enums;

// docs/specs/modele-donnees.md, table « Les cinq domaines RSE ». Réplique exacte de
// frontend/src/types/questionnaire.ts (DOMAIN_LABELS) : un même domaine doit porter le même
// libellé partout, y compris dans le rapport PDF (rapport-pdf.md, section 4).
public static class RseDomainLabels
{
    private static readonly IReadOnlyDictionary<RseDomain, string> Labels = new Dictionary<RseDomain, string>
    {
        [RseDomain.Environmental] = "Environnement",
        [RseDomain.Social] = "Social & droits humains",
        [RseDomain.Ethics] = "Éthique des affaires",
        [RseDomain.Procurement] = "Achats responsables",
        [RseDomain.Governance] = "Gouvernance & pilotage",
    };

    public static string For(RseDomain domain) => Labels[domain];
}
