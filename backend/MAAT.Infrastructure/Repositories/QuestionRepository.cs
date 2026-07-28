using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class QuestionRepository(MaatDbContext context) : IQuestionRepository
{
    public async Task<IReadOnlyList<Question>> FindAllActiveAsync(CancellationToken ct) =>
        await context.Questions.Where(q => q.IsActive).ToListAsync(ct);

    public Task<Question?> FindByCodeAsync(string code, CancellationToken ct) =>
        context.Questions.FirstOrDefaultAsync(q => q.Code == code, ct);
}
