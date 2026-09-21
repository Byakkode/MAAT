using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class SupportTicketRepository(MaatDbContext context) : ISupportTicketRepository
{
    public async Task<List<SupportTicket>> GetByCompanyAsync(Guid companyId, CancellationToken ct) =>
        await context.SupportTickets
            .Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public async Task<SupportTicket?> GetByIdAndCompanyAsync(
        Guid id, Guid companyId, CancellationToken ct) =>
        await context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);

    public void Add(SupportTicket ticket) => context.SupportTickets.Add(ticket);

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
