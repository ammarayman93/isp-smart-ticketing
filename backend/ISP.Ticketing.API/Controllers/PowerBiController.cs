using ISP.Ticketing.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public sealed class PowerBiController(IPowerBiService powerBiService) : ControllerBase
{
    [HttpGet("embed-config")]
    public async Task<IActionResult> GetEmbedConfig(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await powerBiService.GetEmbedConfigAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Power BI is not configured",
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
