using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// docs/specs/norme-volontaire.md, section 3 et cas de test 1 à 10.
public class VsmeCompletenessTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    // Cas 1 : entreprise de 25 salariés, tout renseigné.
    [Fact]
    public void Tout_renseigne_rapport_conforme()
    {
        var result = Evaluate(CompleteStatement(), CompleteIndicators(), [LocatedSite()]);

        Assert.True(result.IsCompliant);
        Assert.Equal(11, result.CompleteCount);
        Assert.All(result.Disclosures, d => Assert.Empty(d.Missing));
    }

    // Cas 2.
    [Fact]
    public void Scope_2_manquant_rend_B3_incomplete_et_le_rapport_partiel()
    {
        var result = Evaluate(CompleteStatement(), CompleteIndicators() with { Scope2LocationTco2e = null }, [LocatedSite()]);

        var b3 = result.For(VsmeDisclosure.B3);
        Assert.Equal(DisclosureState.Incomplete, b3.State);
        Assert.Equal(["Émissions Scope 2"], b3.Missing);
        Assert.False(result.IsCompliant);
        Assert.Equal(10, result.CompleteCount);
    }

    // Cas 3 : données facultatives jusqu'à 10 salariés (§8).
    [Fact]
    public void Micro_entreprise_sans_energie_eau_ni_dechets_reste_complete()
    {
        var indicators = CompleteIndicators() with
        {
            PermanentEmployees = 6,
            TemporaryEmployees = 2,
            FemaleEmployees = 4,
            MaleEmployees = 4,
            EnergyConsumptionKwh = null,
            Scope1Tco2e = null,
            Scope2LocationTco2e = null,
            WaterWithdrawalM3 = null,
            HazardousWasteTons = null,
            NonHazardousWasteTons = null,
            RecyclingRatePct = null,
        };
        var statement = CompleteStatement() with { CircularEconomyApplied = null };

        var result = Evaluate(statement, indicators, [LocatedSite()]);

        Assert.True(result.IsMicro);
        Assert.Equal(DisclosureState.Complete, result.For(VsmeDisclosure.B3).State);
        Assert.Equal(DisclosureState.Complete, result.For(VsmeDisclosure.B6).State);
        Assert.Equal(DisclosureState.Complete, result.For(VsmeDisclosure.B7).State);
        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void Sans_effectif_saisi_la_tranche_Micro_decide()
    {
        var indicators = CompleteIndicators() with
        {
            PermanentEmployees = null,
            TemporaryEmployees = null,
            EmployeeCountFte = null,
            WaterWithdrawalM3 = null,
        };

        var micro = Evaluate(CompleteStatement(), indicators, [LocatedSite()], CompanySizeRange.Micro);
        var small = Evaluate(CompleteStatement(), indicators, [LocatedSite()], CompanySizeRange.Small);

        Assert.True(micro.IsMicro);
        Assert.Equal(DisclosureState.Complete, micro.For(VsmeDisclosure.B6).State);
        Assert.False(small.IsMicro);
        Assert.Equal(DisclosureState.Incomplete, small.For(VsmeDisclosure.B6).State);
    }

    // Cas 4 : B4 est une information « si applicable » (§15, §34).
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void Pollution_non_applicable_B4_complete(bool? applicable)
    {
        var result = Evaluate(CompleteStatement() with { PollutionReportingApplicable = applicable }, CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(DisclosureState.Complete, result.For(VsmeDisclosure.B4).State);
    }

    [Fact]
    public void Pollution_applicable_sans_polluant_ni_lien_B4_incomplete()
    {
        var statement = CompleteStatement() with { PollutionReportingApplicable = true, Pollutants = [], PollutionReportUrl = null };

        var result = Evaluate(statement, CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(DisclosureState.Incomplete, result.For(VsmeDisclosure.B4).State);
    }

    [Fact]
    public void Pollution_applicable_avec_un_lien_B4_complete()
    {
        var statement = CompleteStatement() with { PollutionReportingApplicable = true, PollutionReportUrl = "https://exemple.fr/declaration-gerep.pdf" };

        var result = Evaluate(statement, CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(DisclosureState.Complete, result.For(VsmeDisclosure.B4).State);
    }

    // Cas 5 : omission au titre du §22.
    [Fact]
    public void Information_omise_compte_comme_traitee()
    {
        var statement = CompleteStatement() with { OmittedDisclosures = [VsmeDisclosure.B3] };
        var indicators = CompleteIndicators() with { Scope1Tco2e = null, Scope2LocationTco2e = null, EnergyConsumptionKwh = null };

        var result = Evaluate(statement, indicators, [LocatedSite()]);

        Assert.Equal(DisclosureState.Omitted, result.For(VsmeDisclosure.B3).State);
        Assert.True(result.IsCompliant);
    }

    [Fact]
    public void B1_ne_peut_pas_etre_omise()
    {
        var statement = CompleteStatement() with { OmittedDisclosures = [VsmeDisclosure.B1], LegalForm = null };

        var result = Evaluate(statement, CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(DisclosureState.Incomplete, result.For(VsmeDisclosure.B1).State);
    }

    // Cas 6.
    [Fact]
    public void Sans_site_B1_et_B5_incompletes()
    {
        var result = Evaluate(CompleteStatement(), CompleteIndicators(), []);

        Assert.Contains("Au moins un site", result.For(VsmeDisclosure.B1).Missing);
        Assert.Equal(DisclosureState.Incomplete, result.For(VsmeDisclosure.B5).State);
    }

    [Fact]
    public void Site_sans_coordonnees_B1_incomplete()
    {
        var site = new CompanySite(Guid.NewGuid(), SiteDetails(), Now);

        var result = Evaluate(CompleteStatement(), CompleteIndicators(), [site]);

        Assert.Equal(["Géolocalisation du site « Siège »"], result.For(VsmeDisclosure.B1).Missing);
    }

    // Cas 7.
    [Fact]
    public void Site_sans_reponse_zone_sensible_B5_incomplete()
    {
        var site = LocatedSite(SiteDetails() with { InOrNearSensitiveArea = null });

        var result = Evaluate(CompleteStatement(), CompleteIndicators(), [site]);

        Assert.Equal(DisclosureState.Incomplete, result.For(VsmeDisclosure.B5).State);
    }

    [Fact]
    public void Site_en_zone_sensible_sans_nom_de_zone_B5_incomplete()
    {
        var site = LocatedSite(SiteDetails() with { InOrNearSensitiveArea = true, SensitiveAreaName = null });

        var result = Evaluate(CompleteStatement(), CompleteIndicators(), [site]);

        Assert.Equal(["Nom de la zone sensible du site « Siège »"], result.For(VsmeDisclosure.B5).Missing);
    }

    [Fact]
    public void Rapport_consolide_sans_filiale_B1_incomplete()
    {
        var statement = CompleteStatement() with { ReportingBasis = ReportingBasis.Consolidated, Subsidiaries = [] };

        var result = Evaluate(statement, CompleteIndicators(), [LocatedSite()]);

        Assert.Contains("Filiales incluses dans le périmètre", result.For(VsmeDisclosure.B1).Missing);
    }

    [Fact]
    public void B2_exige_une_reponse_aux_quatre_questions()
    {
        var statement = CompleteStatement() with { HasTargets = null };

        var result = Evaluate(statement, CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(["Objectifs de suivi"], result.For(VsmeDisclosure.B2).Missing);
    }

    [Fact]
    public void Sans_declaration_ni_indicateur_rien_n_est_complet_sauf_B4_et_B11()
    {
        var result = VsmeCompleteness.Evaluate(null, null, [], CompanySizeRange.Small);

        Assert.False(result.IsCompliant);
        Assert.Equal(
            [VsmeDisclosure.B4, VsmeDisclosure.B11],
            result.Disclosures.Where(d => d.State != DisclosureState.Incomplete).Select(d => d.Disclosure));
    }

    [Fact]
    public void Onze_informations_dans_l_ordre_de_la_norme()
    {
        var result = Evaluate(CompleteStatement(), CompleteIndicators(), [LocatedSite()]);

        Assert.Equal(Enum.GetValues<VsmeDisclosure>(), result.Disclosures.Select(d => d.Disclosure));
    }

    // Cas 8 : règles de cohérence de RseIndicators.
    [Fact]
    public void Totaux_recalcules_quand_toutes_les_parties_sont_saisies()
    {
        var record = new RseIndicators(Guid.NewGuid(), 2025);

        record.Update(new RseIndicatorValues
        {
            EnergyConsumptionKwh = 1,
            ElectricityRenewableMwh = 10,
            ElectricityNonRenewableMwh = 20,
            FuelsRenewableMwh = 0,
            FuelsNonRenewableMwh = 5.5,
            Co2EmissionsTons = 1,
            Scope1Tco2e = 12,
            Scope2LocationTco2e = 3,
            WasteTons = 1,
            HazardousWasteTons = 0.5,
            NonHazardousWasteTons = 4,
        });

        Assert.Equal(35_500, record.EnergyConsumptionKwh);
        Assert.Equal(15, record.Co2EmissionsTons);
        Assert.Equal(4.5, record.WasteTons);
    }

    [Fact]
    public void Totaux_saisis_conserves_quand_une_partie_manque()
    {
        var record = new RseIndicators(Guid.NewGuid(), 2025);

        record.Update(new RseIndicatorValues { EnergyConsumptionKwh = 42_000, ElectricityRenewableMwh = 10, Co2EmissionsTons = 9, Scope1Tco2e = 4 });

        Assert.Equal(42_000, record.EnergyConsumptionKwh);
        Assert.Equal(9, record.Co2EmissionsTons);
    }

    // Cas 9.
    [Fact]
    public void Taux_d_accidents_pour_200_000_heures()
    {
        Assert.Equal(4.0, VsmeMetrics.AccidentRate(3, 150_000));
        Assert.Null(VsmeMetrics.AccidentRate(3, null));
        Assert.Null(VsmeMetrics.AccidentRate(3, 0));
        Assert.Null(VsmeMetrics.AccidentRate(null, 150_000));
    }

    // Cas 10.
    [Theory]
    [InlineData("6201Z", "62.01")]
    [InlineData("0111Z", "01.11")]
    [InlineData("4941A", "49.41")]
    [InlineData("62", null)]
    public void Code_NACE_derive_du_code_NAF(string naf, string? nace)
    {
        Assert.Equal(nace, VsmeMetrics.NaceCode(naf));
    }

    [Fact]
    public void Effectif_somme_des_contrats_sinon_ETP()
    {
        var withContracts = RecordWith(new RseIndicatorValues { PermanentEmployees = 20, TemporaryEmployees = 5, EmployeeCountFte = 22 });
        var fteOnly = RecordWith(new RseIndicatorValues { PermanentEmployees = 20, EmployeeCountFte = 22 });

        Assert.Equal(25, VsmeMetrics.EmployeeCount(withContracts));
        Assert.Equal(22, VsmeMetrics.EmployeeCount(fteOnly));
        Assert.Null(VsmeMetrics.EmployeeCount(null));
    }

    // ---------------------------------------------------------------------------------------

    private static VsmeCompletenessResult Evaluate(
        VsmeStatementValues statement,
        RseIndicatorValues indicators,
        IReadOnlyList<CompanySite> sites,
        CompanySizeRange sizeRange = CompanySizeRange.Small)
    {
        var entity = new VsmeStatement(Guid.NewGuid(), 2025, Now);
        entity.Update(statement, Now);
        return VsmeCompleteness.Evaluate(entity, RecordWith(indicators), sites, sizeRange);
    }

    private static RseIndicators RecordWith(RseIndicatorValues values)
    {
        var record = new RseIndicators(Guid.NewGuid(), 2025);
        record.Update(values);
        return record;
    }

    private static VsmeStatementValues CompleteStatement() => new()
    {
        ReportingBasis = ReportingBasis.Individual,
        LegalForm = "SAS",
        TotalAssetsEur = 2_400_000,
        PrimaryCountry = "France",
        EmployeeCountUnit = EmployeeCountUnit.Headcount,
        HasPractices = true,
        HasPolicies = true,
        PoliciesPublic = false,
        HasFutureInitiatives = true,
        HasTargets = false,
        PollutionReportingApplicable = false,
        CircularEconomyApplied = false,
        MinimumWageMet = true,
    };

    private static RseIndicatorValues CompleteIndicators() => new()
    {
        RevenueEur = 3_100_000,
        EnergyConsumptionKwh = 180_000,
        Scope1Tco2e = 21,
        Scope2LocationTco2e = 6,
        WaterWithdrawalM3 = 900,
        HazardousWasteTons = 0.4,
        NonHazardousWasteTons = 12,
        RecyclingRatePct = 55,
        PermanentEmployees = 22,
        TemporaryEmployees = 3,
        FemaleEmployees = 11,
        MaleEmployees = 14,
        RecordableAccidents = 1,
        HoursWorked = 38_000,
        WorkFatalities = 0,
        CollectiveBargainingPct = 100,
        TrainingHoursPerEmployee = 14,
    };

    private static CompanySiteDetails SiteDetails() =>
        new("Siège", "12 rue de la Paix 75002 Paris", SiteTenure.Leased, InOrNearSensitiveArea: false, SensitiveAreaName: null);

    private static CompanySite LocatedSite(CompanySiteDetails? details = null)
    {
        var site = new CompanySite(Guid.NewGuid(), details ?? SiteDetails(), Now);
        site.Locate(48.8686, 2.3308, "12 Rue de la Paix 75002 Paris");
        return site;
    }
}
