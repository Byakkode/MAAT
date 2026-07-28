using MAAT.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Persistence;

public class EfUnitOfWork(MaatDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct) =>
        await context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await operation(ct);
        await transaction.CommitAsync(ct);
    }
}
