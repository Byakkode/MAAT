namespace MAAT.Application.Exceptions;

// docs/specs/rapport-pdf.md, section 7. Le message est affiché tel quel à l'écran : il dit
// quoi corriger, pas ce qui a échoué en interne.
public sealed class InvalidLogoImageException(string message) : Exception(message);
