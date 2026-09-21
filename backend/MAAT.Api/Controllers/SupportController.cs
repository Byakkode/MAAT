using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Api.Controllers;

public sealed class CreateTicketRequest
{
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public List<IFormFile>? Attachments { get; init; }
}

// DTOs de réponse — définis localement, non exposés à d'autres couches.
file sealed record TicketSummaryResponse(
    Guid Id,
    string Title,
    string TicketType,
    int GithubIssueNumber,
    DateTimeOffset CreatedAt,
    string? State,
    int CommentsCount);

file sealed record CommentResponse(string AuthorLogin, string Body, DateTimeOffset CreatedAt);

file sealed record TicketDetailResponse(
    Guid Id,
    string Title,
    string TicketType,
    string Description,
    int GithubIssueNumber,
    DateTimeOffset CreatedAt,
    string? State,
    IReadOnlyList<CommentResponse> Comments);

[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController(
    IGitHubIssueService gitHubIssueService,
    MaatDbContext db,
    ILogger<SupportController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".json", ".csv", ".xml", ".yaml", ".yml",
            ".png", ".jpg", ".jpeg", ".gif", ".webp",
        };

    [HttpPost("tickets")]
    [EnableRateLimiting("ticket-creation")]
    public async Task<IActionResult> CreateTicket([FromForm] CreateTicketRequest request, CancellationToken ct)
    {
        var files = request.Attachments ?? [];

        if (files.Count > 5)
            return BadRequest(new { message = "Maximum 5 pièces jointes par ticket." });

        foreach (var file in files)
        {
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = $"La pièce jointe « {file.FileName} » dépasse 5 Mo." });

            if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
                return BadRequest(new { message = $"Type de fichier non autorisé : {Path.GetExtension(file.FileName)}." });
        }

        var attachments = new List<(string FileName, string ContentType, byte[] Data)>(files.Count);
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            attachments.Add((file.FileName, file.ContentType, ms.ToArray()));
        }

        var subClaim = User.FindFirst("sub")?.Value;
        var companyIdClaim = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(subClaim, out var userId) || !Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var fullDescription = $"{request.Description}\n\n---\n*Soumis via MAAT Support — utilisateur : {userId} / entreprise : {companyId}*";

        try
        {
            var created = await gitHubIssueService.CreateIssueAsync(
                request.Title, request.Type, fullDescription, attachments, ct);

            var ticket = new SupportTicket(
                companyId, userId,
                created.Number, created.HtmlUrl,
                request.Title, request.Description, request.Type);

            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync(ct);

            return Created($"/api/support/tickets/{ticket.Id}", new { ticketId = ticket.Id });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("GitHub:Token"))
        {
            logger.LogError("Ticket de support non créé : GitHub:Token non configuré.");
            return StatusCode(503, new { message = "Le service de tickets est temporairement indisponible." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Erreur GitHub API lors de la création d'un ticket de support.");
            return StatusCode(502, new { message = "Impossible de créer le ticket sur GitHub." });
        }
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var tickets = await db.SupportTickets
            .Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        // Récupère l'état GitHub en parallèle pour chaque ticket.
        var stateTasks = tickets.Select(t => gitHubIssueService.GetIssueStateAsync(t.GithubIssueNumber, ct));
        var states = await Task.WhenAll(stateTasks);

        var result = tickets.Zip(states, (ticket, state) =>
            new TicketSummaryResponse(
                ticket.Id,
                ticket.Title,
                ticket.TicketType,
                ticket.GithubIssueNumber,
                ticket.CreatedAt,
                state?.State,
                state?.CommentsCount ?? 0));

        return Ok(result);
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Unauthorized();

        var ticket = await db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);

        if (ticket is null)
            return NotFound();

        var stateTask = gitHubIssueService.GetIssueStateAsync(ticket.GithubIssueNumber, ct);
        var commentsTask = gitHubIssueService.GetIssueCommentsAsync(ticket.GithubIssueNumber, ct);

        await Task.WhenAll(stateTask, commentsTask);

        var state = await stateTask;
        var githubComments = await commentsTask;

        var comments = githubComments
            .Select(c => new CommentResponse(c.AuthorLogin, c.Body, c.CreatedAt))
            .ToList();

        return Ok(new TicketDetailResponse(
            ticket.Id,
            ticket.Title,
            ticket.TicketType,
            ticket.Description,
            ticket.GithubIssueNumber,
            ticket.CreatedAt,
            state?.State,
            comments));
    }
}
