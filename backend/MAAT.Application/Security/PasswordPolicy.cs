namespace MAAT.Application.Security;

// Partagée entre AuthService (inscription) et AccountService (changement de mot de passe) :
// une seule règle métier, pour qu'elle ne puisse pas dériver silencieusement entre les deux
// chemins qui la vérifient.
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
}
