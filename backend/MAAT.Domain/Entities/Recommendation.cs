using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class Recommendation
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = default!;
    public RseDomain Domain { get; set; }
    public string ActionText { get; set; } = default!;
    public string? DetailText { get; set; }
    public decimal ImpactPoints { get; set; }
    public EffortLevel EffortLevel { get; set; }
    public string TriggerQuestionCode { get; set; } = default!;
    public int TriggerMaxValue { get; set; }
    public bool IsActive { get; set; } = true;

    private Recommendation()
    {
    }

    public Recommendation(
        string code,
        RseDomain domain,
        string actionText,
        decimal impactPoints,
        EffortLevel effortLevel,
        string triggerQuestionCode,
        int triggerMaxValue)
    {
        if (triggerMaxValue is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(triggerMaxValue), triggerMaxValue, "Le seuil de déclenchement doit être compris entre 0 et 5.");
        }

        Id = Guid.NewGuid();
        Code = code;
        Domain = domain;
        ActionText = actionText;
        ImpactPoints = impactPoints;
        EffortLevel = effortLevel;
        TriggerQuestionCode = triggerQuestionCode;
        TriggerMaxValue = triggerMaxValue;
    }
}
