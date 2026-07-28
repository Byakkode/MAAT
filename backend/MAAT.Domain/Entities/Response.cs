namespace MAAT.Domain.Entities;

public class Response
{
    public Guid Id { get; private set; }
    public Guid DiagnosticId { get; set; }
    public Guid QuestionId { get; set; }
    public int Value { get; private set; }
    public DateTimeOffset AnsweredAt { get; private set; }

    private Response()
    {
    }

    public Response(Guid diagnosticId, Guid questionId, int value)
    {
        Id = Guid.NewGuid();
        DiagnosticId = diagnosticId;
        QuestionId = questionId;
        UpdateValue(value);
    }

    // Sauvegarde automatique (questionnaire.md, section 4) : un upsert appelle ceci sur la
    // ligne existante plutôt que d'en construire une nouvelle, mais la validation doit
    // s'appliquer identiquement aux deux chemins — d'où la centralisation ici plutôt que
    // dans le seul constructeur.
    public void UpdateValue(int value)
    {
        if (value is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "La valeur d'une réponse doit être comprise entre 0 et 5.");
        }

        Value = value;
        AnsweredAt = DateTimeOffset.UtcNow;
    }
}
