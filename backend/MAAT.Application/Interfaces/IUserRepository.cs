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
    Task<int> DeleteAllForCompanyAsync(Guid companyId, CancellationToken ct);
}
