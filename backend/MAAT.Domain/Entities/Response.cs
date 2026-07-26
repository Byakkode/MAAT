namespace MAAT.Domain.Entities;

public class Response
{
    public Guid Id { get; private set; }
    public Guid DiagnosticId { get; set; }
    public Guid QuestionId { get; set; }
    public int Value { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }

    private Response()
    {
    }

    public Response(Guid diagnosticId, Guid questionId, int value)
    {
        if (value is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "La valeur d'une réponse doit être comprise entre 0 et 5.");
        }

        Id = Guid.NewGuid();
        DiagnosticId = diagnosticId;
        QuestionId = questionId;
        Value = value;
        AnsweredAt = DateTimeOffset.UtcNow;
    }
}
