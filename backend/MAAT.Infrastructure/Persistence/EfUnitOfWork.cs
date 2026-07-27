using MAAT.Application.Interfaces;

namespace MAAT.Infrastructure.Persistence;

public class EfUnitOfWork(MaatDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct) =>
        await context.SaveChangesAsync(ct);
}
