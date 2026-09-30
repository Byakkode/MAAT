namespace MAAT.Domain.Enums;

// Intitulés des informations du module de base, traduits de l'annexe I du règlement délégué
// (UE) 2026/1560. Mêmes libellés que frontend/src/pages/IndicatorsPage.tsx : l'écran de
// saisie et le rapport doivent nommer une information de la même façon.
public static class VsmeDisclosureLabels
{
    private static readonly IReadOnlyDictionary<VsmeDisclosure, string> Labels = new Dictionary<VsmeDisclosure, string>
    {
        [VsmeDisclosure.B1] = "Base d'établissement du rapport",
        [VsmeDisclosure.B2] = "Pratiques, politiques et initiatives futures pour une économie plus durable",
        [VsmeDisclosure.B3] = "Énergie et émissions de gaz à effet de serre",
        [VsmeDisclosure.B4] = "Pollution de l'air, de l'eau et du sol",
        [VsmeDisclosure.B5] = "Biodiversité",
        [VsmeDisclosure.B6] = "Eau",
        [VsmeDisclosure.B7] = "Ressources, économie circulaire et gestion des déchets",
        [VsmeDisclosure.B8] = "Effectifs : caractéristiques générales",
        [VsmeDisclosure.B9] = "Effectifs : santé et sécurité",
        [VsmeDisclosure.B10] = "Effectifs : rémunération, négociation collective et formation",
        [VsmeDisclosure.B11] = "Condamnations et amendes pour corruption",
    };

    public static string For(VsmeDisclosure disclosure) => Labels[disclosure];
}
