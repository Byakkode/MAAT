using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/questionnaire.md, section 2 (GET /api/diagnostics/{id}/questions) : une
// question active avec, le cas échéant, la réponse déjà enregistrée sur ce diagnostic.
// Value est nul tant que la question n'a pas de réponse.
public sealed record QuestionAnswerView(
    string Code,
    string Text,
    string? HelpText,
    RseDomain Domain,
    int DisplayOrder,
    int? Value);
