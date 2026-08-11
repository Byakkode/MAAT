namespace MAAT.Application.Exceptions;

// docs/specs/rapport-pdf.md, section 2, table des préconditions.
public sealed class DiagnosticNotCompletedException()
    : Exception("Le diagnostic n'est pas complété : un rapport ne peut être généré que pour un diagnostic au statut Completed.");

public sealed class EmailNotVerifiedException()
    : Exception("L'adresse e-mail du compte n'est pas vérifiée : la génération de rapport est bloquée tant qu'elle ne l'est pas.");
