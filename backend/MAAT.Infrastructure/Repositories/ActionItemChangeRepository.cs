using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class ActionItemChangeRepository(MaatDbContext context) : IActionItemChangeRepository
{
    public Task<ActionItemChange?> FindLatestAsync(Guid diagnosticId, string recommendationCode, CancellationToken ct) =>
        context.ActionItemChanges
            .Where(c => c.DiagnosticId == diagnosticId && c.RecommendationCode == recommendationCode)
            .OrderByDescending(c => c.ChangedAt)
            .FirstOrDefaultAsync(ct);

    public void Add(ActionItemChange change) => context.ActionItemChanges.Add(change);

    // Jointure externe : l'auteur peut avoir supprimé son compte depuis (ChangedBy null).
    // Tri final en mémoire : plusieurs champs modifiés par la même requête partagent la même
    // date, et doivent se lire dans l'ordre de l'énumération (statut, responsable, échéance,
    // notes) — un ORDER BY sur la colonne texte les rangerait par ordre alphabétique.
    // L'historique d'une seule action reste court : rien à craindre à trier côté serveur.
    public async Task<IReadOnlyList<ActionItemChangeView>> ListAsync(Guid diagnosticId, string recommendationCode, CancellationToken ct)
    {
        var rows = await (
            from change in context.ActionItemChanges
            where change.DiagnosticId == diagnosticId && change.RecommendationCode == recommendationCode
            join user in context.Users on change.ChangedByUserId equals user.Id into authors
            from author in authors.DefaultIfEmpty()
            select new ActionItemChangeView(change.Field, change.OldValue, change.NewValue, change.ChangedAt, author != null ? author.Email : null))
            .ToListAsync(ct);

        return [.. rows.OrderByDescending(r => r.ChangedAt).ThenBy(r => r.Field)];
    }
}
