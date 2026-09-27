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
        // Un jour du calendrier, gardé tel que choisi à minuit UTC : « 2026-11-15 » reçu d'un
        // serveur réglé sur Paris arrive à +01:00, que timestamptz refuse, et une conversion
        // en UTC le ramènerait au 14 (ActionItemProgressTests).
        DueDate = dueDate is { } due ? new DateTimeOffset(due.Year, due.Month, due.Day, 0, 0, 0, TimeSpan.Zero) : null;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
