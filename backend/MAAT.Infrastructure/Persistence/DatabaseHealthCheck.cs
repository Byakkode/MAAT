using MAAT.Application.Interfaces;

namespace MAAT.Infrastructure.Persistence;

public class DatabaseHealthCheck(MaatDbContext context) : IDatabaseHealthCheck
{
    public Task<bool> CanConnectAsync(CancellationToken ct) => context.Database.CanConnectAsync(ct);
}
