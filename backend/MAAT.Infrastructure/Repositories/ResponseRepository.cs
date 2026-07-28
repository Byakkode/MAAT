using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Repositories;

public class ResponseRepository(MaatDbContext context, ICurrentUserContext currentUser) : IResponseRepository
{
    public Task<Response?> FindByIdAsync(Guid responseId, CancellationToken ct) =>
        context.Responses
            .Where(r => r.Id == responseId)
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Response>> FindAllForCurrentCompanyAsync(CancellationToken ct) =>
        await context.Responses
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .ToListAsync(ct);

    public Task<Response?> FindByDiagnosticAndQuestionAsync(Guid diagnosticId, Guid questionId, CancellationToken ct) =>
        context.Responses
            .Where(r => r.DiagnosticId == diagnosticId && r.QuestionId == questionId)
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Response>> FindAllForDiagnosticAsync(Guid diagnosticId, CancellationToken ct) =>
        await context.Responses
            .Where(r => r.DiagnosticId == diagnosticId)
            .Where(r => context.Diagnostics.Any(d => d.Id == r.DiagnosticId && d.CompanyId == currentUser.CompanyId))
            .ToListAsync(ct);

    public async Task AddAsync(Response response, CancellationToken ct) =>
        await context.Responses.AddAsync(response, ct);
}
