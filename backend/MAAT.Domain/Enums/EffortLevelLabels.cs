namespace MAAT.Domain.Enums;

// Réplique exacte de frontend/src/components/dashboard/ActionPlanCard.tsx (EFFORT_LABELS) :
// même raison que RseDomainLabels — un même niveau d'effort doit porter le même libellé sur
// le tableau de bord et dans le rapport PDF.
public static class EffortLevelLabels
{
    private static readonly IReadOnlyDictionary<EffortLevel, string> Labels = new Dictionary<EffortLevel, string>
    {
        [EffortLevel.Low] = "Effort faible",
        [EffortLevel.Medium] = "Effort modéré",
        [EffortLevel.High] = "Effort important",
    };

    public static string For(EffortLevel effortLevel) => Labels[effortLevel];
}
