namespace MAAT.Application.Exceptions;

public sealed class DiagnosticAlreadyInProgressException(Guid existingDiagnosticId)
    : Exception("Un diagnostic est déjà en cours pour cette entreprise.")
{
    public Guid ExistingDiagnosticId { get; } = existingDiagnosticId;
}

public sealed class DiagnosticNotInProgressException()
    : Exception("Ce diagnostic n'est plus en cours.");

public sealed class QuestionNotAvailableException(string questionCode)
    : Exception($"La question '{questionCode}' n'existe pas ou n'est plus active.")
{
    public string QuestionCode { get; } = questionCode;
}

public sealed class IncompleteQuestionnaireException(IReadOnlyList<string> missingQuestionCodes)
    : Exception("Il manque des réponses à des questions actives.")
{
    public IReadOnlyList<string> MissingQuestionCodes { get; } = missingQuestionCodes;
}
