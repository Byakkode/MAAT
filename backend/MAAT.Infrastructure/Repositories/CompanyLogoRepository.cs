using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class CompanyLogoRepository(MaatDbContext context) : ICompanyLogoRepository
{
    public Task<CompanyLogo?> FindByCompanyIdAsync(Guid companyId, CancellationToken ct) =>
        context.CompanyLogos.FirstOrDefaultAsync(l => l.CompanyId == companyId, ct);

    public async Task AddAsync(CompanyLogo logo, CancellationToken ct) =>
        await context.CompanyLogos.AddAsync(logo, ct);

    public void Remove(CompanyLogo logo) =>
        context.CompanyLogos.Remove(logo);
}
