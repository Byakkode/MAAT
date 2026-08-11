using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.UseCases;
using MAAT.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
[Authorize]
public class DiagnosticsController(DiagnosticService diagnosticService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create(CreateDiagnosticRequest? request, CancellationToken ct)
    {
        // request.CompanyId n'est jamais lu (docs/specs/auth-securite-rgpd.md, section 4) :
        // DiagnosticService.CreateAsync détermine l'entreprise depuis le principal authentifié.
        // Le paramètre est donc nullable : un appelant qui n'a rien à transmettre (le cas
        // normal) ne doit pas être forcé d'envoyer un corps — sans ce "?", ASP.NET Core refuse
        // par défaut un corps vide pour un paramètre [FromBody] non nullable (400 "A non-empty
        // request body is required"), même une fois Content-Type: application/json correctement
        // posé côté client.
        try
        {
            var diagnostic = await diagnosticService.CreateAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = diagnostic.Id }, new
            {
                id = diagnostic.Id,
                companyId = diagnostic.CompanyId,
                status = diagnostic.Status.ToString(),
                createdAt = diagnostic.CreatedAt,
            });
        }
        catch (DiagnosticAlreadyInProgressException ex)
        {
            return Conflict(new { message = ex.Message, existingDiagnosticId = ex.ExistingDiagnosticId });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var diagnostic = await diagnosticService.GetByIdAsync(id, ct);
        if (diagnostic is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = diagnostic.Id,
            companyId = diagnostic.CompanyId,
            status = diagnostic.Status.ToString(),
            globalScore = diagnostic.GlobalScore,
            createdAt = diagnostic.CreatedAt,
            completedAt = diagnostic.CompletedAt,
        });
    }

    // docs/specs/questionnaire.md, section 5. Route littérale "current" : jamais en
    // conflit avec {id:guid} ci-dessus, dont la contrainte exclut toute valeur non-GUID.
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var diagnostic = await diagnosticService.GetCurrentInProgressAsync(ct);
        if (diagnostic is null)
        {
            return NotFound();
        }

        var answeredCount = await diagnosticService.CountAnsweredQuestionsAsync(diagnostic.Id, ct);
        var totalActiveQuestions = await diagnosticService.CountActiveQuestionsAsync(ct);

        return Ok(new
        {
            id = diagnostic.Id,
            companyId = diagnostic.CompanyId,
            status = diagnostic.Status.ToString(),
            createdAt = diagnostic.CreatedAt,
            answeredCount,
            totalActiveQuestions,
        });
    }

    [HttpPost("{id:guid}/abandon")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Abandon(Guid id, CancellationToken ct)
    {
        try
        {
            var diagnostic = await diagnosticService.AbandonAsync(id, ct);
            if (diagnostic is null)
            {
                return NotFound();
            }

            return Ok(new
            {
                id = diagnostic.Id,
                companyId = diagnostic.CompanyId,
                status = diagnostic.Status.ToString(),
                createdAt = diagnostic.CreatedAt,
            });
        }
        catch (DiagnosticNotInProgressException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/responses/{questionCode}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> UpsertResponse(Guid id, string questionCode, UpsertResponseRequest request, CancellationToken ct)
    {
        try
        {
            var result = await diagnosticService.UpsertResponseAsync(id, questionCode, request.Value, ct);
            if (result is null)
            {
                return NotFound();
            }

            var (response, created) = result.Value;
            var body = new
            {
                id = response.Id,
                diagnosticId = response.DiagnosticId,
                questionId = response.QuestionId,
                value = response.Value,
                answeredAt = response.AnsweredAt,
            };

            return created ? StatusCode(StatusCodes.Status201Created, body) : Ok(body);
        }
        catch (DiagnosticNotInProgressException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (QuestionNotAvailableException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            var diagnostic = await diagnosticService.CompleteAsync(id, ct);
            if (diagnostic is null)
            {
                return NotFound();
            }

            return Ok(new
            {
                id = diagnostic.Id,
                companyId = diagnostic.CompanyId,
                status = diagnostic.Status.ToString(),
                globalScore = diagnostic.GlobalScore,
                createdAt = diagnostic.CreatedAt,
                completedAt = diagnostic.CompletedAt,
            });
        }
        catch (DiagnosticNotInProgressException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (IncompleteQuestionnaireException ex)
        {
            return BadRequest(new { message = ex.Message, missingQuestionCodes = ex.MissingQuestionCodes });
        }
    }

    // docs/specs/questionnaire.md, section 2, cas 21 à 24. Pas de restriction de rôle
    // (contrairement à Create/Abandon/UpsertResponse/Complete) : lecture accessible aux
    // trois rôles, y compris Viewer, et sur un diagnostic Completed.
    [HttpGet("{id:guid}/questions")]
    public async Task<IActionResult> GetQuestions(Guid id, CancellationToken ct)
    {
        var questions = await diagnosticService.GetQuestionsWithAnswersAsync(id, ct);
        if (questions is null)
        {
            return NotFound();
        }

        return Ok(questions.Select(q => new
        {
            code = q.Code,
            text = q.Text,
            helpText = q.HelpText,
            domain = q.Domain.ToString(),
            displayOrder = q.DisplayOrder,
            value = q.Value,
        }));
    }

    // docs/specs/recommandations.md, section 4. Pas de restriction de rôle (contrairement à
    // la bascule de suivi ci-dessous) : lecture accessible aux trois rôles, y compris
    // Viewer, comme GetQuestions.
    [HttpGet("{id:guid}/recommendations")]
    public async Task<IActionResult> GetRecommendations(Guid id, CancellationToken ct)
    {
        var recommendations = await diagnosticService.GetRecommendationsAsync(id, ct);
        if (recommendations is null)
        {
            return NotFound();
        }

        return Ok(recommendations.Select(r => new
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
        }));
    }

    // section 5. Admin et User seulement, y compris sur un diagnostic Completed (c'est même
    // le cas normal) : Viewer → 403.
    [HttpPatch("{id:guid}/recommendations/{recommendationCode}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> UpdateRecommendationProgress(
        Guid id, string recommendationCode, UpdateRecommendationProgressRequest request, CancellationToken ct)
    {
        var entry = await diagnosticService.UpdateRecommendationProgressAsync(id, recommendationCode, request.IsCompleted, ct);
        if (entry is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            diagnosticId = entry.DiagnosticId,
            recommendationId = entry.RecommendationId,
            priorityRank = entry.PriorityRank,
            isCompleted = entry.IsCompleted,
            completedAt = entry.CompletedAt,
        });
    }

    [HttpGet("{diagnosticId:guid}/domain-scores/{domain}")]
    public async Task<IActionResult> GetDomainScore(Guid diagnosticId, RseDomain domain, CancellationToken ct)
    {
        var domainScore = await diagnosticService.GetDomainScoreAsync(diagnosticId, domain, ct);
        if (domainScore is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            diagnosticId = domainScore.DiagnosticId,
            domain = domainScore.Domain.ToString(),
            score = domainScore.Score,
            sectorWeight = domainScore.SectorWeight,
        });
    }

    // docs/specs/rapport-pdf.md, section 2. GET malgré l'écriture d'une ligne Report : le
    // navigateur doit pouvoir suivre le lien directement, l'effet de bord est un journal
    // d'audit, pas une modification de l'état métier (assumé explicitement, pas masqué).
    // Pas de restriction de rôle : les trois rôles, Viewer compris, comme GetQuestions et
    // GetRecommendations ci-dessus — la génération est une lecture.
    [HttpGet("{id:guid}/report")]
    [EnableRateLimiting("report-generation")]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken ct)
    {
        try
        {
            var report = await diagnosticService.GenerateReportAsync(id, ct);
            if (report is null)
            {
                return NotFound();
            }

            return File(report.Bytes, "application/pdf", report.FileName);
        }
        catch (DiagnosticNotCompletedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (EmailNotVerifiedException ex)
        {
            // Forbid() (authentification par schéma) ne convient pas ici : la section 2 exige
            // un motif exploitable par le frontend dans le corps de la réponse, pas seulement
            // un 403 nu.
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
