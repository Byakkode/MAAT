using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    // Purge RGPD (section 6) : supprime tous les User d'une entreprise (cascade DB vers
    // RefreshToken/EmailVerificationToken). Doit s'exécuter après la purge des
    // Diagnostic de la même entreprise — Report.generated_by_user_id référence User en
    // ON DELETE RESTRICT, donc un Report encore présent bloquerait cette suppression.
    // Réservé au dernier Admin qui supprime son compte (coquille-et-compte.md, section 6) :
    // c'est le seul cas qui supprime l'entreprise elle-même.
    Task<int> DeleteAllForCompanyAsync(Guid companyId, CancellationToken ct);

    // docs/specs/coquille-et-compte.md, section 6 : détermine si l'appelant de DELETE /api/me
    // est le dernier administrateur de son entreprise — la seule condition qui déclenche la
    // suppression de l'entreprise entière plutôt que du seul compte de l'appelant.
    Task<int> CountAdminsForCompanyAsync(Guid companyId, CancellationToken ct);

    // Suppression d'un seul compte (tout appelant qui n'est pas le dernier Admin) : les autres
    // comptes de l'entreprise et ses diagnostics restent intacts. Les rapports générés par CE
    // compte doivent être supprimés avant lui (voir IReportRepository), pour la même raison de
    // contrainte ON DELETE RESTRICT que DeleteAllForCompanyAsync ci-dessus.
    Task DeleteAsync(Guid userId, CancellationToken ct);
}
