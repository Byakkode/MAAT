using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class UserRepository(MaatDbContext context) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct) =>
        await context.Users.AddAsync(user, ct);

    public Task<int> DeleteAllForCompanyAsync(Guid companyId, CancellationToken ct) =>
        context.Users.Where(u => u.CompanyId == companyId).ExecuteDeleteAsync(ct);
}
