namespace MAAT.Application.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    // Nécessaire pour la purge RGPD (section 6) : plusieurs suppressions en masse
    // (ExecuteDeleteAsync, qui s'exécute immédiatement sans passer par SaveChanges)
    // doivent réussir ou échouer ensemble — un compte à moitié purgé serait pire
    // qu'un compte pas purgé du tout.
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct);
}
