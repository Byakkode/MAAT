using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/recommandations.md, section 4 bis : une ligne de l'historique telle que l'écran
// l'affiche. ChangedBy : adresse e-mail de l'auteur, null quand son compte a été supprimé.
public sealed record ActionItemChangeView(
    ActionItemField Field,
    string? OldValue,
    string? NewValue,
    DateTimeOffset ChangedAt,
    string? ChangedBy);
