using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

// ADR 0014 et docs/specs/norme-volontaire.md (B5) : détection automatique des zones sensibles
// pour la biodiversité autour d'un site.
public class SensitiveAreaDetectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Une_zone_protegee_a_plusieurs_titres_n_apparait_qu_une_fois()
    {
        // Réponse réelle de l'API Carto autour de la forêt de Fontainebleau : la même forêt dans
        // trois couches, en capitales dans la couche ZNIEFF.
        var text = SensitiveAreaSummary.Format(
        [
            new("MASSIF DE FONTAINEBLEAU", SensitiveAreaKind.Znieff1),
            new("Massif de Fontainebleau", SensitiveAreaKind.NaturaBirds),
            new("Massif de Fontainebleau", SensitiveAreaKind.NaturaHabitats),
        ]);

        Assert.Equal("Massif de Fontainebleau (Natura 2000 Habitats, Natura 2000 Oiseaux, ZNIEFF 1)", text);
    }

    [Fact]
    public void Mise_en_forme_independante_de_l_ordre_des_reponses()
    {
        SensitiveArea[] areas =
        [
            new("Marais de la Souche", SensitiveAreaKind.NaturaBirds),
            new("Réserve des Sept-Îles", SensitiveAreaKind.NatureReserve),
            new("Coteaux calcaires", SensitiveAreaKind.Znieff1),
        ];

        Assert.Equal(SensitiveAreaSummary.Format(areas), SensitiveAreaSummary.Format([.. areas.Reverse()]));
        Assert.Equal(
            "Marais de la Souche (Natura 2000 Oiseaux) ; Réserve des Sept-Îles (réserve naturelle) ; Coteaux calcaires (ZNIEFF 1)",
            SensitiveAreaSummary.Format(areas));
    }

    [Fact]
    public void Aucune_zone_donne_null_et_les_zones_au_dela_de_quatre_sont_comptees()
    {
        Assert.Null(SensitiveAreaSummary.Format([]));

        var six = Enumerable.Range(1, 6).Select(i => new SensitiveArea($"Zone {i}", SensitiveAreaKind.Znieff1)).ToList();
        Assert.EndsWith("Zone 4 (ZNIEFF 1) ; et 2 autres", SensitiveAreaSummary.Format(six));
    }

    [Fact]
    public void La_reponse_de_l_utilisateur_prime_sur_la_detection()
    {
        var site = LocatedSite(inOrNear: false);
        site.RecordSensitiveAreaCheck("Massif de Fontainebleau (Natura 2000 Habitats)", Now);

        Assert.False(site.EffectiveInOrNearSensitiveArea);
        Assert.Null(site.EffectiveSensitiveAreaName);
        Assert.False(site.IsSensitiveAreaFromDetection);
    }

    [Fact]
    public void Sans_reponse_la_detection_repond()
    {
        var found = LocatedSite(inOrNear: null);
        found.RecordSensitiveAreaCheck("Massif de Fontainebleau (Natura 2000 Habitats)", Now);
        var none = LocatedSite(inOrNear: null);
        none.RecordSensitiveAreaCheck(null, Now);
        var unchecked_ = LocatedSite(inOrNear: null);

        Assert.True(found.EffectiveInOrNearSensitiveArea);
        Assert.Equal("Massif de Fontainebleau (Natura 2000 Habitats)", found.EffectiveSensitiveAreaName);
        Assert.True(found.IsSensitiveAreaFromDetection);
        Assert.False(none.EffectiveInOrNearSensitiveArea);
        Assert.True(none.IsSensitiveAreaFromDetection);
        Assert.Null(unchecked_.EffectiveInOrNearSensitiveArea);
        Assert.False(unchecked_.IsSensitiveAreaFromDetection);
    }

    [Fact]
    public void Utilisateur_qui_confirme_sans_nommer_la_zone_reprend_le_nom_detecte()
    {
        var site = LocatedSite(inOrNear: true);
        site.RecordSensitiveAreaCheck("Massif de Fontainebleau (Natura 2000 Habitats)", Now);

        Assert.Equal("Massif de Fontainebleau (Natura 2000 Habitats)", site.EffectiveSensitiveAreaName);
    }

    [Fact]
    public void Changer_d_adresse_efface_la_detection()
    {
        var site = LocatedSite(inOrNear: null);
        site.RecordSensitiveAreaCheck("Massif de Fontainebleau (Natura 2000 Habitats)", Now);

        site.Update(new CompanySiteDetails("Siège", "3 quai des Chartrons 33000 Bordeaux", SiteTenure.Leased, null, null), Now);

        Assert.False(site.IsCheckedForSensitiveAreas);
        Assert.Null(site.DetectedSensitiveAreas);
        Assert.Null(site.EffectiveInOrNearSensitiveArea);
    }

    [Fact]
    public void B5_complete_grace_a_la_detection()
    {
        var detectedNone = LocatedSite(inOrNear: null);
        detectedNone.RecordSensitiveAreaCheck(null, Now);
        var detectedFound = LocatedSite(inOrNear: null);
        detectedFound.RecordSensitiveAreaCheck("Massif de Fontainebleau (Natura 2000 Habitats)", Now);
        var notChecked = LocatedSite(inOrNear: null);

        Assert.Equal(DisclosureState.Complete, B5([detectedNone, detectedFound]));
        Assert.Equal(DisclosureState.Incomplete, B5([detectedNone, notChecked]));
    }

    private static DisclosureState B5(IReadOnlyList<CompanySite> sites) =>
        VsmeCompleteness.Evaluate(null, null, sites, CompanySizeRange.Small).For(VsmeDisclosure.B5).State;

    private static CompanySite LocatedSite(bool? inOrNear)
    {
        var site = new CompanySite(Guid.NewGuid(), new CompanySiteDetails("Siège", "Route de la Plaine 77300 Fontainebleau", SiteTenure.Owned, inOrNear, null), Now);
        site.Locate(48.425, 2.64, "Route de la Plaine 77300 Fontainebleau");
        return site;
    }
}
