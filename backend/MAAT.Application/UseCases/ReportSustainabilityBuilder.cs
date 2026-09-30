using System.Globalization;
using MAAT.Application.DTOs;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/norme-volontaire.md, sections 2 et 6 : traduit les données d'un exercice en
// informations B1 à B11 prêtes à mettre en page. Pur (aucune I/O, aucune horloge) : le
// générateur PDF reste déterministe (rapport-pdf.md, section 3). Chaque donnée absente dit
// pourquoi elle l'est ; le rapport ne laisse jamais une case blanche.
public static class ReportSustainabilityBuilder
{
    public sealed record Input(
        int Year,
        Company Company,
        VsmeStatement? Statement,
        RseIndicators? Indicators,
        VsmeStatement? PreviousStatement,
        RseIndicators? PreviousIndicators,
        IReadOnlyList<CompanySite> Sites);

    public static ReportSustainability Build(Input input)
    {
        var completeness = VsmeCompleteness.Evaluate(input.Statement, input.Indicators, input.Sites, input.Company.SizeRange);
        var context = new Context(input, completeness.IsMicro);
        var hasPrevious = input.PreviousStatement is not null || input.PreviousIndicators is not null;

        var disclosures = completeness.Disclosures
            .Select(c =>
            {
                var (datapoints, tables) = c.State == DisclosureState.Omitted
                    ? ([], [])
                    : Compose(c.Disclosure, context);
                return new ReportDisclosure(c.Disclosure, VsmeDisclosureLabels.For(c.Disclosure), c.State, c.Missing, datapoints, tables);
            })
            .ToList();

        return new ReportSustainability(
            input.Year,
            hasPrevious ? input.Year - 1 : null,
            completeness.IsMicro,
            completeness.IsCompliant,
            completeness.CompleteCount,
            disclosures,
            ReportIndicatorCatalog.Build(input.Year, input.Indicators, input.PreviousIndicators));
    }

    private sealed record Context(Input Input, bool IsMicro)
    {
        public VsmeStatement? S => Input.Statement;
        public RseIndicators? I => Input.Indicators;
        public VsmeStatement? PS => Input.PreviousStatement;
        public RseIndicators? PI => Input.PreviousIndicators;

        // Donnée essentielle, sauf pour une micro-entreprise quand elle est facultative (§8).
        public DatapointAbsence Essential(bool optionalForMicro = false) =>
            optionalForMicro && IsMicro ? DatapointAbsence.OptionalForMicro : DatapointAbsence.NotProvided;
    }

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) Compose(VsmeDisclosure disclosure, Context c) =>
        disclosure switch
        {
            VsmeDisclosure.B1 => B1(c),
            VsmeDisclosure.B2 => (B2(c), []),
            VsmeDisclosure.B3 => B3(c),
            VsmeDisclosure.B4 => B4(c),
            VsmeDisclosure.B5 => B5(c),
            VsmeDisclosure.B6 => (B6(c), []),
            VsmeDisclosure.B7 => (B7(c), []),
            VsmeDisclosure.B8 => B8(c),
            VsmeDisclosure.B9 => (B9(c), []),
            VsmeDisclosure.B10 => (B10(c), []),
            _ => (B11(c), []),
        };

    // ---------------------------------------------------------------------------------------

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) B1(Context c)
    {
        var s = c.S;
        var employees = VsmeMetrics.EmployeeCount(c.I);
        var nace = VsmeMetrics.NaceCode(c.Input.Company.SectorCode);

        var datapoints = new List<ReportDatapoint>
        {
            new("Option retenue", Text: "Option A : module de base"),
            TextOrAbsent("Base d'établissement", s?.ReportingBasis switch
            {
                ReportingBasis.Individual => "Individuelle (l'entreprise seule)",
                ReportingBasis.Consolidated => "Consolidée (l'entreprise et ses filiales)",
                _ => null,
            }, c.Essential()),
            TextOrAbsent("Forme juridique", s?.LegalForm, c.Essential()),
            new("Code NACE", Text: nace is null ? $"NAF {c.Input.Company.SectorCode}" : $"{nace} (NAF {c.Input.Company.SectorCode})"),
            Number("Total du bilan", s?.TotalAssetsEur, "€", c.PS?.TotalAssetsEur, c.Essential()),
            Number("Chiffre d'affaires", c.I?.RevenueEur, "€", c.PI?.RevenueEur, c.Essential()),
            // Somme des contrats de B8, exprimée dans l'unité de B8 ; à défaut, l'effectif en ETP.
            Number("Effectif", employees, c.I is { PermanentEmployees: not null, TemporaryEmployees: not null } ? EmployeeUnit(s) : "ETP",
                VsmeMetrics.EmployeeCount(c.PI), c.Essential(), decimals: 1),
            TextOrAbsent("Pays principal d'activité", s?.PrimaryCountry, c.Essential()),
            new("Informations omises (§22)", Text: s is { OmittedDisclosures.Count: > 0 }
                ? string.Join(", ", s.OmittedDisclosures.Select(d => d.ToString()))
                : "Aucune"),
        };

        var tables = new List<ReportTable>();
        tables.Add(c.Input.Sites.Count == 0
            ? new ReportTable("Sites", ["Site"], [[new ReportCell("Aucun site déclaré")]])
            : new ReportTable(
                "Sites détenus, loués ou gérés",
                ["Site", "Adresse", "Statut", "Latitude", "Longitude"],
                [.. c.Input.Sites.Select(site => (IReadOnlyList<ReportCell>)
                [
                    site.Name,
                    site.GeocodedLabel ?? site.Address,
                    TenureLabel(site.Tenure),
                    site.Latitude is { } lat ? new ReportCell(Number: lat, Decimals: 5) : "Non localisé",
                    site.Longitude is { } lon ? new ReportCell(Number: lon, Decimals: 5) : "Non localisé",
                ])]));

        if (s?.ReportingBasis == ReportingBasis.Consolidated)
        {
            tables.Add(new ReportTable(
                "Filiales incluses dans le rapport",
                ["Filiale", "Adresse du siège"],
                s.Subsidiaries.Count == 0
                    ? [["Non renseigné", ""]]
                    : [.. s.Subsidiaries.Select(sub => (IReadOnlyList<ReportCell>)[sub.Name, sub.RegisteredAddress])]));
        }

        tables.Add(s is { Certifications.Count: > 0 }
            ? new ReportTable(
                "Certifications et labels de durabilité (§28)",
                ["Label", "Organisme", "Date", "Note"],
                [.. s.Certifications.Select(cert => (IReadOnlyList<ReportCell>)
                [
                    cert.Name,
                    cert.Issuer,
                    cert.ObtainedOn?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    cert.Rating,
                ])])
            : new ReportTable("Certifications et labels de durabilité (§28)", ["Label"], [["Aucun label déclaré"]]));

        return (datapoints, tables);
    }

    private static IReadOnlyList<ReportDatapoint> B2(Context c)
    {
        var s = c.S;
        var datapoints = new List<ReportDatapoint>
        {
            YesNo("Pratiques en place pour une économie plus durable", s?.HasPractices, c.Essential()),
            YesNo("Politiques en matière de durabilité", s?.HasPolicies, c.Essential()),
        };

        if (s?.HasPolicies == true)
        {
            datapoints.Add(YesNo("Politiques rendues publiques", s.PoliciesPublic, c.Essential()));
        }

        datapoints.Add(YesNo("Initiatives futures ou plans en cours", s?.HasFutureInitiatives, c.Essential()));
        datapoints.Add(YesNo("Objectifs de suivi de la mise en œuvre", s?.HasTargets, c.Essential()));

        if (s is { CoveredTopics.Count: > 0 })
        {
            datapoints.Add(new("Thèmes couverts", Text: string.Join(" ; ", s.CoveredTopics.Select(SustainabilityTopicLabels.For))));
        }

        if (s?.PracticesDescription is { } description)
        {
            datapoints.Add(new("Description", Text: description));
        }

        return datapoints;
    }

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) B3(Context c)
    {
        var i = c.I;
        var optional = c.Essential(optionalForMicro: true);
        var datapoints = new List<ReportDatapoint>
        {
            Number("Consommation totale d'énergie", i?.EnergyConsumptionKwh / 1000, "MWh", c.PI?.EnergyConsumptionKwh / 1000, optional, decimals: 1),
            Number("Émissions brutes Scope 1", i?.Scope1Tco2e, "tCO₂eq", c.PI?.Scope1Tco2e, optional, decimals: 1),
            Number("Émissions brutes Scope 2 (localisation)", i?.Scope2LocationTco2e, "tCO₂eq", c.PI?.Scope2LocationTco2e, optional, decimals: 1),
        };

        // Total : somme des deux scopes quand ils sont saisis (RseIndicators.ApplyTotals), sinon
        // le total déclaré sans ventilation, qui reste une information utile au lecteur.
        if (i?.Co2EmissionsTons is not null)
        {
            var label = i is { Scope1Tco2e: not null, Scope2LocationTco2e: not null } ? "Total Scope 1 et 2" : "Émissions totales déclarées (sans ventilation)";
            datapoints.Add(Number(label, i.Co2EmissionsTons, "tCO₂eq", c.PI?.Co2EmissionsTons, optional, decimals: 1));
        }

        var tables = new List<ReportTable>();
        if (i is not null && (i.ElectricityRenewableMwh ?? i.ElectricityNonRenewableMwh ?? i.FuelsRenewableMwh ?? i.FuelsNonRenewableMwh) is not null)
        {
            tables.Add(new ReportTable(
                "Ventilation de la consommation d'énergie (MWh)",
                ["", "Renouvelable", "Non renouvelable", "Total"],
                [
                    EnergyRow("Électricité (facturée)", i.ElectricityRenewableMwh, i.ElectricityNonRenewableMwh),
                    EnergyRow("Combustibles", i.FuelsRenewableMwh, i.FuelsNonRenewableMwh),
                ]));
        }

        return (datapoints, tables);
    }

    private static IReadOnlyList<ReportCell> EnergyRow(string label, double? renewable, double? nonRenewable) =>
    [
        label,
        Cell(renewable, 1),
        Cell(nonRenewable, 1),
        renewable is { } r && nonRenewable is { } n ? Cell(r + n, 1) : new ReportCell(),
    ];

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) B4(Context c)
    {
        var s = c.S;
        if (s?.PollutionReportingApplicable != true)
        {
            return ([new("Rejets de polluants à déclarer", Absence: DatapointAbsence.NotApplicable,
                AbsenceNote: "aucune obligation de déclaration ni système de management environnemental")], []);
        }

        var datapoints = new List<ReportDatapoint>();
        if (s.PollutionReportUrl is { } url)
        {
            datapoints.Add(new("Déclaration publique", Text: url));
        }

        var tables = new List<ReportTable>();
        if (s.Pollutants.Count > 0)
        {
            tables.Add(new ReportTable(
                "Polluants rejetés",
                ["Polluant", "Milieu", "Quantité"],
                [.. s.Pollutants.Select(p => (IReadOnlyList<ReportCell>)
                [
                    p.Name,
                    p.Medium switch { PollutionMedium.Air => "Air", PollutionMedium.Water => "Eau", _ => "Sol" },
                    new ReportCell(Number: p.Quantity, Decimals: 3, Unit: p.Unit),
                ])]));
        }
        else if (datapoints.Count == 0)
        {
            datapoints.Add(new("Polluants rejetés", Absence: DatapointAbsence.NotProvided));
        }

        return (datapoints, tables);
    }

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) B5(Context c)
    {
        var sites = c.Input.Sites;
        if (sites.Count == 0)
        {
            return ([new("Sites dans ou près d'une zone sensible", Absence: DatapointAbsence.NotProvided)], []);
        }

        var sensitive = sites.Where(s => s.InOrNearSensitiveArea == true).ToList();
        var unanswered = sites.Count(s => s.InOrNearSensitiveArea is null);
        var datapoints = new List<ReportDatapoint>
        {
            new("Sites dans ou près d'une zone sensible", Value: sensitive.Count, Unit: $"sur {sites.Count}"),
        };

        if (unanswered > 0)
        {
            datapoints.Add(new("Sites non évalués", Value: unanswered, Unit: "site(s)"));
        }

        IReadOnlyList<ReportTable> tables = sensitive.Count == 0
            ? []
            : [new ReportTable(
                "Sites concernés",
                ["Site", "Zone sensible"],
                [.. sensitive.Select(s => (IReadOnlyList<ReportCell>)[s.Name, s.SensitiveAreaName ?? "Non renseigné"])])];

        return (datapoints, tables);
    }

    private static IReadOnlyList<ReportDatapoint> B6(Context c)
    {
        var i = c.I;
        var datapoints = new List<ReportDatapoint>
        {
            Number("Prélèvement total d'eau", i?.WaterWithdrawalM3, "m³", c.PI?.WaterWithdrawalM3, c.Essential(optionalForMicro: true)),
        };

        // §37 : seulement si des procédés de production consomment beaucoup d'eau.
        datapoints.Add(i?.WaterConsumptionM3 is null
            ? new("Consommation d'eau", Absence: DatapointAbsence.NotApplicable, AbsenceNote: "pas de procédé fortement consommateur d'eau")
            : Number("Consommation d'eau", i.WaterConsumptionM3, "m³", c.PI?.WaterConsumptionM3, c.Essential(optionalForMicro: true)));

        if (i?.WaterConsumptionM3 is not null)
        {
            datapoints.Add(Number("dont en zone de stress hydrique", i.WaterConsumptionStressM3, "m³", c.PI?.WaterConsumptionStressM3, c.Essential(optionalForMicro: true)));
        }

        return datapoints;
    }

    private static IReadOnlyList<ReportDatapoint> B7(Context c)
    {
        var (s, i) = (c.S, c.I);
        var optional = c.Essential(optionalForMicro: true);
        var datapoints = new List<ReportDatapoint>
        {
            YesNo("Principes d'économie circulaire appliqués", s?.CircularEconomyApplied, optional),
        };

        if (s?.CircularEconomyDescription is { } how)
        {
            datapoints.Add(new("Mise en œuvre", Text: how));
        }

        datapoints.Add(Number("Déchets dangereux", i?.HazardousWasteTons, "t", c.PI?.HazardousWasteTons, optional, decimals: 2));
        datapoints.Add(Number("Déchets non dangereux", i?.NonHazardousWasteTons, "t", c.PI?.NonHazardousWasteTons, optional, decimals: 2));
        if (i?.WasteTons is not null)
        {
            datapoints.Add(Number("Total des déchets produits", i.WasteTons, "t", c.PI?.WasteTons, optional, decimals: 2));
        }

        datapoints.Add(Number("Part recyclée ou préparée pour réutilisation", i?.RecyclingRatePct, "%", c.PI?.RecyclingRatePct, optional, decimals: 1));
        datapoints.Add(s?.MaterialFlowsDescription is { } flows
            ? new("Flux annuel des matières", Text: flows)
            : new("Flux annuel des matières", Absence: DatapointAbsence.NotApplicable, AbsenceNote: "secteur sans flux de matières importants"));

        return datapoints;
    }

    private static (IReadOnlyList<ReportDatapoint>, IReadOnlyList<ReportTable>) B8(Context c)
    {
        var (s, i) = (c.S, c.I);
        var unit = EmployeeUnit(s);
        var datapoints = new List<ReportDatapoint>
        {
            TextOrAbsent("Unité de décompte", s?.EmployeeCountUnit switch
            {
                EmployeeCountUnit.Headcount => "Effectif en personnes",
                EmployeeCountUnit.FullTimeEquivalent => "Équivalents temps plein",
                _ => null,
            }, c.Essential()),
            Number("Contrat permanent", i?.PermanentEmployees, unit, c.PI?.PermanentEmployees, c.Essential(), decimals: 1),
            Number("Contrat temporaire", i?.TemporaryEmployees, unit, c.PI?.TemporaryEmployees, c.Essential(), decimals: 1),
            Number("Femmes", i?.FemaleEmployees, unit, c.PI?.FemaleEmployees, c.Essential(), decimals: 1),
            Number("Hommes", i?.MaleEmployees, unit, c.PI?.MaleEmployees, c.Essential(), decimals: 1),
        };

        if (i?.OtherGenderEmployees is not null)
        {
            datapoints.Add(Number("Autre", i.OtherGenderEmployees, unit, c.PI?.OtherGenderEmployees, c.Essential(), decimals: 1));
        }

        IReadOnlyList<ReportTable> tables = s is { EmployeesByCountry.Count: > 0 }
            ? [new ReportTable(
                "Effectif par pays du contrat de travail",
                ["Pays", "Effectif"],
                [.. s.EmployeesByCountry.Select(e => (IReadOnlyList<ReportCell>)[e.Country, new ReportCell(Number: e.Employees, Decimals: 1, Unit: unit)])])]
            : [];

        return (datapoints, tables);
    }

    private static IReadOnlyList<ReportDatapoint> B9(Context c)
    {
        var i = c.I;
        return
        [
            Number("Accidents du travail enregistrables", i?.RecordableAccidents, "accident(s)", c.PI?.RecordableAccidents, c.Essential()),
            Number(
                "Taux d'accidents",
                VsmeMetrics.AccidentRate(i?.RecordableAccidents, i?.HoursWorked),
                "pour 200 000 h travaillées",
                VsmeMetrics.AccidentRate(c.PI?.RecordableAccidents, c.PI?.HoursWorked),
                c.Essential(),
                decimals: 2),
            Number("Décès liés au travail", i?.WorkFatalities, "décès", c.PI?.WorkFatalities, c.Essential()),
        ];
    }

    private static IReadOnlyList<ReportDatapoint> B10(Context c)
    {
        var (s, i) = (c.S, c.I);
        return
        [
            YesNo("Rémunération au moins égale au salaire minimum applicable", s?.MinimumWageMet, c.Essential()),
            // §42 b : seulement si la loi impose déjà de le publier.
            i?.GenderPayGapPct is null
                ? new("Écart de rémunération femmes-hommes", Absence: DatapointAbsence.NotApplicable, AbsenceNote: "pas d'obligation légale de publication")
                : Number("Écart de rémunération femmes-hommes", i.GenderPayGapPct, "%", c.PI?.GenderPayGapPct, c.Essential(), decimals: 1),
            Number("Salariés couverts par une convention collective", i?.CollectiveBargainingPct, "%", c.PI?.CollectiveBargainingPct, c.Essential(), decimals: 1),
            Number("Heures de formation par salarié", i?.TrainingHoursPerEmployee, "h/an", c.PI?.TrainingHoursPerEmployee, c.Essential(), decimals: 1),
        ];
    }

    private static IReadOnlyList<ReportDatapoint> B11(Context c)
    {
        var s = c.S;
        if (s?.CorruptionConvictions is not > 0)
        {
            return [new("Condamnations pour corruption sur l'exercice", Value: 0, Unit: "condamnation")];
        }

        return
        [
            Number("Condamnations pour corruption sur l'exercice", s.CorruptionConvictions, "condamnation(s)", c.PS?.CorruptionConvictions, DatapointAbsence.NotProvided),
            Number("Montant total des amendes", s.CorruptionFinesEur, "€", c.PS?.CorruptionFinesEur, DatapointAbsence.NotProvided),
        ];
    }

    // ---------------------------------------------------------------------------------------

    private static ReportDatapoint Number(string label, double? value, string unit, double? previous, DatapointAbsence absence, int decimals = 0) =>
        value is null
            ? new ReportDatapoint(label, Unit: unit, Absence: absence)
            : new ReportDatapoint(label, value, unit, previous, decimals);

    private static ReportDatapoint TextOrAbsent(string label, string? text, DatapointAbsence absence) =>
        text is null ? new ReportDatapoint(label, Absence: absence) : new ReportDatapoint(label, Text: text);

    private static ReportDatapoint YesNo(string label, bool? answer, DatapointAbsence absence) =>
        TextOrAbsent(label, answer switch { true => "Oui", false => "Non", null => null }, absence);

    private static ReportCell Cell(double? value, int decimals) => value is null ? new ReportCell() : new ReportCell(Number: value, Decimals: decimals);

    private static string EmployeeUnit(VsmeStatement? s) =>
        s?.EmployeeCountUnit == EmployeeCountUnit.FullTimeEquivalent ? "ETP" : "salarié(s)";

    private static string TenureLabel(SiteTenure tenure) => tenure switch
    {
        SiteTenure.Owned => "Détenu",
        SiteTenure.Leased => "Loué",
        _ => "Géré",
    };
}
