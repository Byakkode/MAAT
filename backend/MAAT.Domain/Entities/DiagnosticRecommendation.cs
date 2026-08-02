namespace MAAT.Domain.Entities;

public class DiagnosticRecommendation
{
    public Guid DiagnosticId { get; private set; }
    public Guid RecommendationId { get; private set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int PriorityRank { get; set; }

    private DiagnosticRecommendation()
    {
    }

    public DiagnosticRecommendation(Guid diagnosticId, Guid recommendationId, int priorityRank)
    {
        DiagnosticId = diagnosticId;
        RecommendationId = recommendationId;
        PriorityRank = priorityRank;
    }

    // docs/specs/recommandations.md, section 5 : bascule is_completed et renseigne ou
    // efface completed_at ensemble — jamais l'un sans l'autre, d'où la centralisation ici
    // plutôt qu'une affectation directe des deux propriétés dans le service applicatif
    // (même principe que Response.UpdateValue).
    public void SetProgress(bool isCompleted)
    {
        IsCompleted = isCompleted;
        CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null;
    }
}
