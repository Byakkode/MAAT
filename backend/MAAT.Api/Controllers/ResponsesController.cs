using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/responses")]
[Authorize]
public class ResponsesController(DiagnosticService diagnosticService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await diagnosticService.GetResponseByIdAsync(id, ct);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = response.Id,
            diagnosticId = response.DiagnosticId,
            questionId = response.QuestionId,
            value = response.Value,
            answeredAt = response.AnsweredAt,
        });
    }
}
