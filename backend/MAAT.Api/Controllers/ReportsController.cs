using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(DiagnosticService diagnosticService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var report = await diagnosticService.GetReportByIdAsync(id, ct);
        if (report is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = report.Id,
            diagnosticId = report.DiagnosticId,
            format = report.Format.ToString(),
            generatedAt = report.GeneratedAt,
            generatedByUserId = report.GeneratedByUserId,
        });
    }
}
