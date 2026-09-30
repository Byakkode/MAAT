namespace MAAT.Domain.Enums;

// Annexe B de la norme volontaire. Mêmes libellés que frontend/src/pages/IndicatorsPage.tsx.
public static class SustainabilityTopicLabels
{
    private static readonly IReadOnlyDictionary<SustainabilityTopic, string> Labels = new Dictionary<SustainabilityTopic, string>
    {
        [SustainabilityTopic.ClimateChange] = "Changement climatique",
        [SustainabilityTopic.Pollution] = "Pollution",
        [SustainabilityTopic.Water] = "Eau",
        [SustainabilityTopic.Biodiversity] = "Biodiversité et écosystèmes",
        [SustainabilityTopic.CircularEconomy] = "Économie circulaire et utilisation des ressources",
        [SustainabilityTopic.Workforce] = "Effectifs et travailleurs de la chaîne de valeur",
        [SustainabilityTopic.AffectedCommunities] = "Communautés affectées",
        [SustainabilityTopic.ConsumersAndEndUsers] = "Consommateurs et utilisateurs finaux",
        [SustainabilityTopic.BusinessConduct] = "Conduite des affaires",
    };

    public static string For(SustainabilityTopic topic) => Labels[topic];
}
