using MAAT.Application.DTOs;
using MAAT.Domain.Entities;

namespace MAAT.Application.UseCases;

// docs/specs/rapport-pdf.md, section 4, bloc « Indicateurs RSE ». Traduit un enregistrement
// RseIndicators en lignes affichables, dans un ordre fixe (déterminisme, section 3). Libellés
// et unités identiques à frontend/src/pages/IndicatorsPage.tsx : un indicateur qui porte deux
// noms entre l'écran et le document ferait douter du chiffre.
public static class ReportIndicatorCatalog
{
    private sealed record Entry(string Group, string Label, string Unit, bool? HigherIsBetter, Func<RseIndicators, double?> Read);

    private static readonly Entry[] Entries =
    [
        new("Environnement", "Émissions CO₂", "tCO₂eq/an", false, r => r.Co2EmissionsTons),
        new("Environnement", "Consommation énergie", "kWh/an", false, r => r.EnergyConsumptionKwh),
        new("Environnement", "Part énergie renouvelable", "%", true, r => r.RenewableEnergyPct),
        new("Environnement", "Consommation eau", "m³/an", false, r => r.WaterConsumptionM3),
        new("Environnement", "Déchets produits", "t/an", false, r => r.WasteTons),
        new("Environnement", "Taux de recyclage", "%", true, r => r.RecyclingRatePct),

        new("Social", "Effectif", "ETP", null, r => r.EmployeeCountFte),
        new("Social", "Taux de turnover", "%", false, r => r.TurnoverRatePct),
        new("Social", "Formation / salarié", "h/an", true, r => r.TrainingHoursPerEmployee),
        new("Social", "Taux d'accidents du travail", "‰", false, r => r.WorkAccidentRate),
        new("Social", "Index égalité F/H", "/100", true, r => r.GenderEqualityIndex),
        new("Social", "Part CDI", "%", true, r => r.PermanentContractPct),

        new("Achats responsables", "Fournisseurs locaux (< 100 km)", "%", true, r => r.LocalSuppliersPct),
        new("Achats responsables", "Fournisseurs évalués RSE", "%", true, r => r.RseAssessedSuppliersPct),
        new("Achats responsables", "Nombre de fournisseurs actifs", "fournisseurs", null, r => r.ActiveSuppliersCount),

        new("Économique", "Chiffre d'affaires", "€", null, r => r.RevenueEur),
        new("Économique", "Investissements RSE", "€", true, r => r.RseInvestmentEur),
        new("Économique", "Part CA export", "%", null, r => r.ExportRevenuePct),
    ];

    // Null quand aucun indicateur n'est renseigné pour l'année : le rapport affiche alors une
    // invitation à les saisir plutôt qu'un tableau vide.
    public static ReportIndicators? Build(RseIndicators current, RseIndicators? previous)
    {
        var items = Entries
            .Select(e => new ReportIndicator(e.Group, e.Label, e.Unit, e.Read(current), previous is null ? null : e.Read(previous), e.HigherIsBetter))
            .ToList();

        return items.Any(i => i.Value is not null)
            ? new ReportIndicators(current.Year, previous?.Year, items)
            : null;
    }
}
