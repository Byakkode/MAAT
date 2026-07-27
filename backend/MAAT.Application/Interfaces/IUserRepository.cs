using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);
}
