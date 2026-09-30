using MAAT.Domain.Services;

namespace MAAT.Application.Interfaces;

// ADR 0014 : zones sensibles pour la biodiversité autour d'un point (norme volontaire, B5).
// Null quand le service n'a pas pu répondre en entier : une réponse partielle ferait croire à
// l'absence de zone là où une couche n'a simplement pas été lue. L'implémentation ne lève
// jamais, l'enregistrement d'un site ne dépend pas d'un service externe.
public interface ISensitiveAreaLocator
{
    Task<IReadOnlyList<SensitiveArea>?> FindNearAsync(double latitude, double longitude, CancellationToken ct);
}
