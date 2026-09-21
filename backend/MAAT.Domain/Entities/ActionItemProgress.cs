using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class ActionItemProgress
{
    public Guid Id { get; private set; }
    public Guid DiagnosticId { get; private set; }
    public string RecommendationCode { get; private set; } = default!;
    public ActionItemStatus Status { get; private set; } = ActionItemStatus.Planned;
    public string? AssignedTo { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ActionItemProgress() { }

    public static ActionItemProgress Create(Guid diagnosticId, string code) => new()
    {
        Id = Guid.NewGuid(),
        DiagnosticId = diagnosticId,
        RecommendationCode = code,
        Status = ActionItemStatus.Planned,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    public void Update(ActionItemStatus status, string? assignedTo, DateTimeOffset? dueDate, string? notes)
    {
        Status = status;
        AssignedTo = assignedTo;
        DueDate = dueDate;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
