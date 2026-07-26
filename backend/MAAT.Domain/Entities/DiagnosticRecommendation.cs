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
}
