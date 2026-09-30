namespace MAAT.Application.Exceptions;

// docs/specs/norme-volontaire.md, section 4. Le message est affiché tel quel à l'écran : il
// dit quoi corriger.
public sealed class InvalidVsmeDataException(string message) : Exception(message);

public sealed class SiteLimitReachedException(int limit)
    : Exception($"Une entreprise peut déclarer {limit} sites au plus.");
