using MAAT.Domain.Enums;

namespace MAAT.Application.Interfaces;

// Seule source autorisée pour company_id (docs/specs/auth-securite-rgpd.md, section 4,
// "ne pas faire confiance au company_id transmis par le client") : résolu depuis les
// claims du principal authentifié, jamais depuis le corps, l'URL ou un en-tête de requête.
public interface ICurrentUserContext
{
    Guid UserId { get; }

    Guid CompanyId { get; }

    UserRole Role { get; }
}
