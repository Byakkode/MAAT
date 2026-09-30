using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

public enum DisclosureState
{
    Complete,
    Incomplete,
    // Déclarée comme omise au titre du §22 (secret des affaires, information protégée).
    Omitted,
}

public sealed record DisclosureCompleteness(VsmeDisclosure Disclosure, DisclosureState State, IReadOnlyList<string> Missing);

public sealed record VsmeCompletenessResult(IReadOnlyList<DisclosureCompleteness> Disclosures, bool IsMicro)
{
    // Complètes ou omises : ce que l'écran et le rapport annoncent comme « traitées ».
    public int CompleteCount => Disclosures.Count(d => d.State != DisclosureState.Incomplete);

    // §27 a : « a module shall be complied with in its entirety ».
    public bool IsCompliant => Disclosures.All(d => d.State != DisclosureState.Incomplete);

    public DisclosureCompleteness For(VsmeDisclosure disclosure) => Disclosures.Single(d => d.Disclosure == disclosure);
}

// docs/specs/norme-volontaire.md, section 3. Service pur, sans I/O : l'écran Indicateurs et la
// section 04 du rapport appellent le même calcul, et ne peuvent donc pas annoncer deux
// complétudes différentes. Les libellés des données manquantes sont affichés tels quels.
public static class VsmeCompleteness
{
    public static VsmeCompletenessResult Evaluate(
        VsmeStatement? statement,
        RseIndicators? indicators,
        IReadOnlyList<CompanySite> sites,
        CompanySizeRange sizeRange)
    {
        var isMicro = VsmeMetrics.IsMicro(VsmeMetrics.EmployeeCount(indicators), sizeRange);
        var omitted = statement?.OmittedDisclosures ?? [];

        var disclosures = new List<DisclosureCompleteness>
        {
            // B1 est la base du rapport, c'est elle qui liste les omissions : jamais omise.
            Build(VsmeDisclosure.B1, [], B1(statement, indicators, sites)),
            Build(VsmeDisclosure.B2, omitted, B2(statement)),
            Build(VsmeDisclosure.B3, omitted, isMicro ? [] : B3(indicators)),
            Build(VsmeDisclosure.B4, omitted, B4(statement)),
            Build(VsmeDisclosure.B5, omitted, B5(sites)),
            Build(VsmeDisclosure.B6, omitted, isMicro ? [] : Required(indicators?.WaterWithdrawalM3, "Prélèvement d'eau")),
            Build(VsmeDisclosure.B7, omitted, isMicro ? [] : B7(statement, indicators)),
            Build(VsmeDisclosure.B8, omitted, B8(statement, indicators)),
            Build(VsmeDisclosure.B9, omitted, B9(indicators)),
            Build(VsmeDisclosure.B10, omitted, B10(statement, indicators)),
            // B11 : sans condamnation sur l'exercice, rien à déclarer (§15, §43).
            Build(VsmeDisclosure.B11, omitted, []),
        };

        return new VsmeCompletenessResult(disclosures, isMicro);
    }

    private static DisclosureCompleteness Build(VsmeDisclosure disclosure, IReadOnlyCollection<VsmeDisclosure> omitted, IReadOnlyList<string> missing)
    {
        if (omitted.Contains(disclosure))
        {
            return new DisclosureCompleteness(disclosure, DisclosureState.Omitted, []);
        }

        return new DisclosureCompleteness(disclosure, missing.Count == 0 ? DisclosureState.Complete : DisclosureState.Incomplete, missing);
    }

    private static List<string> B1(VsmeStatement? s, RseIndicators? i, IReadOnlyList<CompanySite> sites)
    {
        var missing = new List<string>();
        Add(missing, s?.ReportingBasis, "Base individuelle ou consolidée");
        if (s?.ReportingBasis == ReportingBasis.Consolidated && s.Subsidiaries.Count == 0)
        {
            missing.Add("Filiales incluses dans le périmètre");
        }

        Add(missing, s?.LegalForm, "Forme juridique");
        Add(missing, s?.TotalAssetsEur, "Total du bilan");
        Add(missing, i?.RevenueEur, "Chiffre d'affaires");
        Add(missing, VsmeMetrics.EmployeeCount(i), "Effectif");
        Add(missing, s?.PrimaryCountry, "Pays principal d'activité");

        if (sites.Count == 0)
        {
            missing.Add("Au moins un site");
        }

        missing.AddRange(sites.Where(site => !site.IsGeolocated).Select(site => $"Géolocalisation du site « {site.Name} »"));
        return missing;
    }

    private static List<string> B2(VsmeStatement? s)
    {
        var missing = new List<string>();
        Add(missing, s?.HasPractices, "Pratiques en place");
        Add(missing, s?.HasPolicies, "Politiques");
        if (s?.HasPolicies == true)
        {
            Add(missing, s.PoliciesPublic, "Publicité des politiques");
        }

        Add(missing, s?.HasFutureInitiatives, "Initiatives futures");
        Add(missing, s?.HasTargets, "Objectifs de suivi");
        return missing;
    }

    private static List<string> B3(RseIndicators? i)
    {
        var missing = new List<string>();
        Add(missing, i?.EnergyConsumptionKwh, "Consommation totale d'énergie");
        Add(missing, i?.Scope1Tco2e, "Émissions Scope 1");
        Add(missing, i?.Scope2LocationTco2e, "Émissions Scope 2");
        return missing;
    }

    // §34 : seulement si l'entreprise doit déjà déclarer ses rejets (loi, système de management
    // environnemental). Une liste de polluants ou le lien vers la déclaration publique suffit.
    private static List<string> B4(VsmeStatement? s) =>
        s?.PollutionReportingApplicable == true && s.Pollutants.Count == 0 && s.PollutionReportUrl is null
            ? ["Polluants déclarés ou lien vers la déclaration"]
            : [];

    private static List<string> B5(IReadOnlyList<CompanySite> sites)
    {
        if (sites.Count == 0)
        {
            return ["Au moins un site (B1)"];
        }

        // Réponse de l'utilisateur, ou à défaut celle de la détection automatique (ADR 0014).
        var missing = new List<string>();
        foreach (var site in sites)
        {
            if (site.EffectiveInOrNearSensitiveArea is null)
            {
                missing.Add($"Zone sensible pour la biodiversité, site « {site.Name} »");
            }
            else if (site.EffectiveInOrNearSensitiveArea == true && site.EffectiveSensitiveAreaName is null)
            {
                missing.Add($"Nom de la zone sensible du site « {site.Name} »");
            }
        }

        return missing;
    }

    private static List<string> B7(VsmeStatement? s, RseIndicators? i)
    {
        var missing = new List<string>();
        Add(missing, s?.CircularEconomyApplied, "Principes d'économie circulaire");
        Add(missing, i?.HazardousWasteTons, "Déchets dangereux");
        Add(missing, i?.NonHazardousWasteTons, "Déchets non dangereux");
        Add(missing, i?.RecyclingRatePct, "Part recyclée ou réutilisée");
        return missing;
    }

    private static List<string> B8(VsmeStatement? s, RseIndicators? i)
    {
        var missing = new List<string>();
        Add(missing, s?.EmployeeCountUnit, "Unité de décompte (personnes ou ETP)");
        Add(missing, i?.PermanentEmployees, "Salariés en contrat permanent");
        Add(missing, i?.TemporaryEmployees, "Salariés en contrat temporaire");
        Add(missing, i?.FemaleEmployees, "Femmes");
        Add(missing, i?.MaleEmployees, "Hommes");
        return missing;
    }

    private static List<string> B9(RseIndicators? i)
    {
        var missing = new List<string>();
        Add(missing, i?.RecordableAccidents, "Nombre d'accidents du travail");
        Add(missing, i?.HoursWorked, "Heures travaillées (pour le taux)");
        Add(missing, i?.WorkFatalities, "Nombre de décès");
        return missing;
    }

    private static List<string> B10(VsmeStatement? s, RseIndicators? i)
    {
        var missing = new List<string>();
        Add(missing, s?.MinimumWageMet, "Salaire minimum respecté");
        Add(missing, i?.CollectiveBargainingPct, "Couverture par une convention collective");
        Add(missing, i?.TrainingHoursPerEmployee, "Heures de formation par salarié");
        return missing;
    }

    private static List<string> Required(double? value, string label) => value is null ? [label] : [];

    private static void Add<TValue>(List<string> missing, TValue? value, string label)
        where TValue : struct
    {
        if (value is null)
        {
            missing.Add(label);
        }
    }

    private static void Add(List<string> missing, string? value, string label)
    {
        if (value is null)
        {
            missing.Add(label);
        }
    }
}

// Valeurs dérivées du module de base, partagées par la complétude et le rapport.
public static class VsmeMetrics
{
    // Base du taux d'accidents retenue par le guide de l'EFRAG (norme-volontaire.md, section 2).
    public const double AccidentRateHoursBase = 200_000;

    // B1 §27 e v : somme des contrats de B8 quand elle est connue, sinon l'effectif en ETP.
    public static double? EmployeeCount(RseIndicators? indicators) =>
        indicators is { PermanentEmployees: { } permanent, TemporaryEmployees: { } temporary }
            ? permanent + temporary
            : indicators?.EmployeeCountFte;

    // §8 : « 10 employees or less ». À défaut d'effectif saisi, la tranche déclarée à
    // l'inscription (Micro = moins de 10 salariés).
    public static bool IsMicro(double? employeeCount, CompanySizeRange sizeRange) =>
        employeeCount is { } count ? count <= 10 : sizeRange == CompanySizeRange.Micro;

    public static double? AccidentRate(int? accidents, double? hoursWorked) =>
        accidents is { } count && hoursWorked is > 0 ? count / hoursWorked.Value * AccidentRateHoursBase : null;

    // Le NAF rév. 2 reprend la NACE rév. 2 sur ses quatre premiers caractères et y ajoute une
    // lettre : 6201Z → 62.01. Null si le code ne permet pas la dérivation.
    public static string? NaceCode(string nafCode) =>
        nafCode.Length >= 4 && char.IsAsciiDigit(nafCode[0]) && char.IsAsciiDigit(nafCode[1])
            && char.IsAsciiDigit(nafCode[2]) && char.IsAsciiDigit(nafCode[3])
            ? $"{nafCode[..2]}.{nafCode[2..4]}"
            : null;
}
