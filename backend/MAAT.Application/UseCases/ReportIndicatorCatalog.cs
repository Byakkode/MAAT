using MAAT.Application.DTOs;
using MAAT.Domain.Entities;

namespace MAAT.Application.UseCases;

// docs/specs/norme-volontaire.md, section 6, bloc « Compléments » : les indicateurs de MAAT que
// le module de base de la norme volontaire ne demande pas, présentés après B11 (§13 de la
// norme). Ceux qu'elle demande figurent dans leur information B1 à B11
// (ReportSustainabilityBuilder), jamais deux fois. Ordre fixe (déterminisme, rapport-pdf.md
// section 3). Libellés et unités identiques à frontend/src/pages/IndicatorsPage.tsx : un
// indicateur qui porte deux noms entre l'écran et le document ferait douter du chiffre.
public static class ReportIndicatorCatalog
{
    private sealed record Entry(string Group, string Label, string Unit, bool? HigherIsBetter, Func<RseIndicators, double?> Read);

    private static readonly Entry[] Entries =
    [
        new("Environnement", "Part énergie renouvelable", "%", true, r => r.RenewableEnergyPct),

        new("Social", "Taux de turnover", "%", false, r => r.TurnoverRatePct),
        new("Social", "Taux de fréquence des accidents", "/ million h", false, r => r.WorkAccidentRate),
        new("Social", "Index égalité F/H", "/100", true, r => r.GenderEqualityIndex),
        new("Social", "Part CDI", "%", true, r => r.PermanentContractPct),

        new("Achats responsables", "Fournisseurs locaux (< 100 km)", "%", true, r => r.LocalSuppliersPct),
        new("Achats responsables", "Fournisseurs évalués RSE", "%", true, r => r.RseAssessedSuppliersPct),
        new("Achats responsables", "Nombre de fournisseurs actifs", "fournisseurs", null, r => r.ActiveSuppliersCount),

        new("Économique", "Investissements RSE", "€", true, r => r.RseInvestmentEur),
        new("Économique", "Part CA export", "%", null, r => r.ExportRevenuePct),
    ];

    // Null quand aucun complément n'est renseigné pour l'exercice : le bloc disparaît plutôt
    // que d'afficher une liste de « non renseigné » pour des données que personne n'exige.
    public static ReportIndicators? Build(int year, RseIndicators? current, RseIndicators? previous)
    {
        if (current is null)
        {
            return null;
        }

        var items = Entries
            .Select(e => new ReportIndicator(e.Group, e.Label, e.Unit, e.Read(current), previous is null ? null : e.Read(previous), e.HigherIsBetter))
            .ToList();

        return items.Any(i => i.Value is not null)
            ? new ReportIndicators(year, previous?.Year, items)
            : null;
    }
}
