using MAAT.Application.Interfaces;
using MAAT.Application.UseCases;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// Suivi enrichi du plan d'actions : statut, responsable, échéance, notes.
// Distinct de GET/PATCH /api/diagnostics/{id}/recommendations qui gère uniquement isCompleted.
public sealed record UpsertProgressRequest(
    ActionItemStatus Status,
    string? AssignedTo,
    DateTimeOffset? DueDate,
    string? Notes);

[ApiController]
[Route("api/diagnostics/{diagnosticId:guid}/action-plan")]
[Authorize]
public class ActionPlanController(
    IActionItemProgressRepository progressRepository,
    DiagnosticService diagnosticService) : ControllerBase
{
    // docs/specs/recommandations.md, section 4 : retourne les recommandations du diagnostic
    // enrichies du suivi ActionItemProgress. Null → 404 si diagnostic inconnu ou hors entreprise.
    [HttpGet]
    public async Task<IActionResult> GetActionPlan(Guid diagnosticId, CancellationToken ct)
    {
        var recommendations = await diagnosticService.GetRecommendationsAsync(diagnosticId, ct);
        if (recommendations is null) return NotFound();

        var progressMap = await progressRepository.GetMapByDiagnosticAsync(diagnosticId, ct);

        return Ok(recommendations.Select(r =>
        {
            progressMap.TryGetValue(r.Code, out var p);
            return new
            {
                code = r.Code,
                actionText = r.ActionText,
                detailText = r.DetailText,
                domain = r.Domain.ToString(),
                effortLevel = r.EffortLevel.ToString(),
                impactPoints = r.ImpactPoints,
                priorityRank = r.PriorityRank,
                isCompleted = r.IsCompleted,
                completedAt = r.CompletedAt,
                status = p?.Status.ToString() ?? nameof(ActionItemStatus.Planned),
                assignedTo = p?.AssignedTo,
                dueDate = p?.DueDate,
                notes = p?.Notes,
                progressUpdatedAt = p?.UpdatedAt,
            };
        }));
    }

    // docs/specs/recommandations.md, section 5 : met à jour (ou crée) le suivi d'une action.
    // Synchronise isCompleted avec le statut Done pour les widgets dashboard.
    [HttpPatch("{code}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> UpsertProgress(
        Guid diagnosticId, string code, UpsertProgressRequest request, CancellationToken ct)
    {
        // UpdateRecommendationProgressAsync valide l'appartenance du diagnostic à l'entreprise
        // et l'existence du code dans ce diagnostic — null dans les deux cas (404).
        var isCompleted = request.Status == ActionItemStatus.Done;
        var entry = await diagnosticService.UpdateRecommendationProgressAsync(
            diagnosticId, code, isCompleted, ct);
        if (entry is null) return NotFound();

        var progress = await progressRepository.GetByDiagnosticAndCodeAsync(diagnosticId, code, ct);

        if (progress is null)
        {
            progress = ActionItemProgress.Create(diagnosticId, code);
            progressRepository.Add(progress);
        }

        progress.Update(request.Status, request.AssignedTo, request.DueDate, request.Notes);
        await progressRepository.SaveAsync(ct);

        return Ok(new
        {
            diagnosticId,
            code,
            status = progress.Status.ToString(),
            assignedTo = progress.AssignedTo,
            dueDate = progress.DueDate,
            notes = progress.Notes,
            progressUpdatedAt = progress.UpdatedAt,
            completedAt = entry.CompletedAt,
        });
    }
}
