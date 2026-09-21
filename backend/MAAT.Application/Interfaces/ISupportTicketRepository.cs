using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface ISupportTicketRepository
{
    Task<List<SupportTicket>> GetByCompanyAsync(Guid companyId, CancellationToken ct);
    Task<SupportTicket?> GetByIdAndCompanyAsync(Guid id, Guid companyId, CancellationToken ct);
    void Add(SupportTicket ticket);
    Task SaveAsync(CancellationToken ct);
}
