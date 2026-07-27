namespace MAAT.Application.Exceptions;

public sealed class WeakPasswordException(string message) : Exception(message);

public sealed class CompromisedPasswordException(string message) : Exception(message);

public sealed class InvalidCredentialsException()
    : Exception("Adresse e-mail ou mot de passe incorrect.");

public sealed class InvalidRefreshTokenException()
    : Exception("Jeton de rafraîchissement invalide ou expiré.");

public sealed class RefreshTokenReuseDetectedException()
    : Exception("Réutilisation d'un jeton de rafraîchissement révoqué détectée : toutes les sessions ont été révoquées.");

public sealed class InvalidVerificationTokenException()
    : Exception("Jeton de vérification d'adresse invalide ou expiré.");
