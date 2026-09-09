using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutagesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] OutageStatus? status, CancellationToken ct)
    {
        var q = db.OutageEvents.AsNoTracking().Include(x => x.AffectedCategory).AsQueryable();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        var rows = await q.OrderByDescending(x => x.DetectedAt).Take(100).Select(x => new {
            x.Id, x.Title, x.Description, x.Region, category = x.AffectedCategory == null ? null : x.AffectedCategory.Name,
            severity = x.Severity.ToString(), status = x.Status.ToString(), x.DetectedAt, x.ResolvedAt,
            x.EstimatedAffectedCustomers, x.RootCause, x.CreatedBySystem
        }).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Roles = "Admin,Supervisor")]
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateOutageRequest request, CancellationToken ct)
    {
        var outage = await db.OutageEvents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (outage is null) return NotFound(new { message = "Outage event not found." });
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<OutageStatus>(request.Status, true, out var status)) {
            outage.Status = status;
            outage.ResolvedAt = status is OutageStatus.Resolved or OutageStatus.Closed ? DateTime.UtcNow : null;
        }
        if (request.Severity.HasValue) outage.Severity = request.Severity.Value;
        if (request.EstimatedAffectedCustomers.HasValue) outage.EstimatedAffectedCustomers = Math.Max(0, request.EstimatedAffectedCustomers.Value);
        if (request.RootCause is not null) outage.RootCause = request.RootCause.Trim();
        if (request.Description is not null) outage.Description = request.Description.Trim();
        outage.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { outage.Id, status = outage.Status.ToString(), severity = outage.Severity.ToString(), outage.ResolvedAt, outage.RootCause, outage.EstimatedAffectedCustomers });
    }
}
public sealed class UpdateOutageRequest
{
    public string? Status { get; set; }
    public OutageSeverity? Severity { get; set; }
    public int? EstimatedAffectedCustomers { get; set; }
    public string? RootCause { get; set; }
    public string? Description { get; set; }
}
