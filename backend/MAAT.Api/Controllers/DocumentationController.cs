using MAAT.Application.UseCases;
using MAAT.Domain.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

// docs/specs/documentation.md, section 4 : base documentaire RSE. Authentifiée comme tout le
// reste de l'application ; tous les rôles. Sommaire et recherche ouverts à toutes les offres,
// lecture d'un article à partir d'Essential (403 plan_required sinon).
[ApiController]
[Route("api/documentation")]
[Authorize]
public class DocumentationController(DocumentationService documentationService) : ControllerBase
{
    // GET /api/documentation?q=carbone&category=Environment&level=Essentials
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? q,
        [FromQuery] KnowledgeCategory? category,
        [FromQuery] KnowledgeLevel? level)
    {
        // Une recherche démesurée n'a pas de sens pour un humain et ferait tokeniser un texte
        // arbitrairement long à chaque requête.
        if (q is { Length: > 200 })
        {
            return BadRequest(new { message = "La recherche ne doit pas dépasser 200 caractères." });
        }

        var counts = documentationService.CountByCategory();

        return Ok(new
        {
            categories = Enum.GetValues<KnowledgeCategory>()
                .Where(counts.ContainsKey)
                .Select(c => new { category = c.ToString(), count = counts[c] }),
            articles = documentationService.Search(q, category, level).Select(a => new
            {
                slug = a.Slug,
                title = a.Title,
                summary = a.Summary,
                category = a.Category.ToString(),
                level = a.Level.ToString(),
                tags = a.Tags,
                readingMinutes = a.ReadingMinutes,
                updatedOn = a.UpdatedOn,
            }),
        });
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        var article = await documentationService.GetAsync(slug, ct);
        if (article is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            slug = article.Slug,
            title = article.Title,
            summary = article.Summary,
            category = article.Category.ToString(),
            level = article.Level.ToString(),
            tags = article.Tags,
            readingMinutes = article.ReadingMinutes,
            updatedOn = article.UpdatedOn,
            sources = article.Sources.Select(s => new { title = s.Title, url = s.Url }),
            body = article.Body,
        });
    }
}
