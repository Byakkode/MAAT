using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    // docs/specs/dashboard.md, section 1 : accessible aux trois rôles (pas de Roles= —
    // même principe que DiagnosticsController.GetQuestions/GetRecommendations), toujours 200
    // — trois états explicites (aucun diagnostic, en cours seul, complété), jamais une erreur.
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dashboard = await dashboardService.GetAsync(ct);

        return Ok(new
        {
            hasCompletedDiagnostic = dashboard.HasCompletedDiagnostic,
            latestDiagnostic = dashboard.LatestDiagnostic is null
                ? null
                : new
                {
                    id = dashboard.LatestDiagnostic.Id,
                    globalScore = dashboard.LatestDiagnostic.GlobalScore,
                    completedAt = dashboard.LatestDiagnostic.CompletedAt,
                    sectorCode = dashboard.LatestDiagnostic.SectorCode,
                },
            domainScores = dashboard.DomainScores.Select(s => new
            {
                domain = s.Domain.ToString(),
                score = s.Score,
                sectorWeight = s.SectorWeight,
                triggeredRecommendationCount = s.TriggeredRecommendationCount,
            }),
            history = dashboard.History.Select(h => new
            {
                completedAt = h.CompletedAt,
                globalScore = h.GlobalScore,
                deltaFromPrevious = h.DeltaFromPrevious,
            }),
            benchmark = dashboard.Benchmark is null
                ? null
                : new
                {
                    available = dashboard.Benchmark.Available,
                    sampleSize = dashboard.Benchmark.SampleSize,
                    percentile = dashboard.Benchmark.Percentile,
                    reason = dashboard.Benchmark.Reason,
                },
            actionPlan = new
            {
                items = dashboard.ActionPlan.Items.Select(r => new
                {
                    code = r.Code,
                    actionText = r.ActionText,
                    domain = r.Domain.ToString(),
                    effortLevel = r.EffortLevel.ToString(),
                    priorityRank = r.PriorityRank,
                    isCompleted = r.IsCompleted,
                }),
                totalCount = dashboard.ActionPlan.TotalCount,
                completedCount = dashboard.ActionPlan.CompletedCount,
            },
            inProgressDiagnostic = dashboard.InProgressDiagnostic is null
                ? null
                : new
                {
                    id = dashboard.InProgressDiagnostic.Id,
                    answeredCount = dashboard.InProgressDiagnostic.AnsweredCount,
                    totalActiveQuestions = dashboard.InProgressDiagnostic.TotalActiveQuestions,
                },
        });
    }
}
