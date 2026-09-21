using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IRseIndicatorsRepository
{
    Task<RseIndicators?> GetByCompanyAndYearAsync(Guid companyId, int year, CancellationToken ct);
    Task<List<int>> GetYearsByCompanyAsync(Guid companyId, CancellationToken ct);
    void Add(RseIndicators record);
    Task SaveAsync(CancellationToken ct);
}
