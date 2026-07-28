namespace MAAT.Application.DTOs;

// CompanyId est volontairement ignoré par DiagnosticService.CreateAsync : seul le
// principal authentifié détermine l'entreprise (docs/specs/auth-securite-rgpd.md,
// section 4, "ne pas faire confiance au company_id transmis par le client"). Le champ
// existe pour documenter — et permettre de tester — ce choix, pas pour être lu.
public sealed record CreateDiagnosticRequest(Guid? CompanyId);
