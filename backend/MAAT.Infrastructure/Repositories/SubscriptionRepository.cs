using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class SubscriptionRepository(MaatDbContext context) : ISubscriptionRepository
{
    public Task<Subscription?> FindByCompanyIdAsync(Guid companyId, CancellationToken ct) =>
        context.Subscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

    public async Task AddAsync(Subscription subscription, CancellationToken ct) =>
        await context.Subscriptions.AddAsync(subscription, ct);
}
