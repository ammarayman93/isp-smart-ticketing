using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var total = await db.Tickets.CountAsync(ct);
        var open = await db.Tickets.CountAsync(x => x.Status != TicketStatus.Closed && x.Status != TicketStatus.Resolved && x.Status != TicketStatus.Cancelled, ct);
        var resolved = await db.Tickets.CountAsync(x => x.Status == TicketStatus.Resolved || x.Status == TicketStatus.Closed, ct);
        var breached = await db.Tickets.CountAsync(x => x.IsSlaBreached, ct);
        var outages = await db.OutageEvents.CountAsync(x => x.Status != OutageStatus.Closed && x.Status != OutageStatus.Resolved, ct);
        var classified = await db.Tickets.CountAsync(x => x.ClassifiedAt != null, ct);
        var assigned = await db.Tickets.CountAsync(x => x.AssignedAt != null, ct);
        var firstResponse = await db.Tickets.CountAsync(x => x.FirstResponseAt != null, ct);
        var resolutionDurations = await db.Tickets.AsNoTracking().Where(x => x.ResolvedAt != null).Select(x => new { x.CreatedAt, ResolvedAt = x.ResolvedAt!.Value }).ToListAsync(ct);
        var avgResolutionMinutes = resolutionDurations.Count == 0 ? 0 : resolutionDurations.Average(x => (x.ResolvedAt - x.CreatedAt).TotalMinutes);

        var byCategory = await db.Tickets.AsNoTracking().GroupBy(x => x.CategoryId).Select(g => new { categoryId = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var byCategoryNamed = byCategory.Select(x => new { category = x.categoryId.HasValue && categories.TryGetValue(x.categoryId.Value, out var n) ? n : "Unclassified", x.count }).ToList();

        var byPriority = await db.Tickets.AsNoTracking().GroupBy(x => x.Priority).Select(g => new { priority = g.Key.ToString(), count = g.Count() }).OrderByDescending(x => x.count).ToListAsync(ct);
        var byStatus = await db.Tickets.AsNoTracking().GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).OrderByDescending(x => x.count).ToListAsync(ct);
        var byRegion = await db.Tickets.AsNoTracking().GroupBy(x => x.CustomerRegion).Select(g => new { region = g.Key ?? "Unknown", count = g.Count() }).OrderByDescending(x => x.count).Take(10).ToListAsync(ct);

        return Ok(new
        {
            total,
            open,
            resolved,
            slaBreached = breached,
            activeOutages = outages,
            classified,
            assigned,
            firstResponse,
            classificationRate = total == 0 ? 0 : Math.Round(classified * 100.0 / total, 2),
            assignmentRate = total == 0 ? 0 : Math.Round(assigned * 100.0 / total, 2),
            firstResponseRate = total == 0 ? 0 : Math.Round(firstResponse * 100.0 / total, 2),
            resolutionRate = total == 0 ? 0 : Math.Round(resolved * 100.0 / total, 2),
            averageResolutionMinutes = Math.Round(avgResolutionMinutes, 1),
            byCategory = byCategoryNamed,
            byPriority,
            byStatus,
            byRegion
        });
    }

    [HttpGet("outages")]
    public async Task<IActionResult> Outages(CancellationToken ct)
    {
        var outages = await db.OutageEvents.AsNoTracking()
            .Include(x => x.AffectedCategory)
            .OrderByDescending(x => x.DetectedAt)
            .Take(50)
            .Select(x => new
            {
                x.Id, x.Title, x.Description, x.Region,
                category = x.AffectedCategory == null ? null : x.AffectedCategory.Name,
                severity = x.Severity.ToString(), status = x.Status.ToString(),
                x.DetectedAt, x.ResolvedAt, x.EstimatedAffectedCustomers, x.CreatedBySystem
            }).ToListAsync(ct);
        return Ok(outages);
    }

    [Authorize(Roles = "Admin,Supervisor")]
    [HttpGet("agent-performance")]
    public async Task<IActionResult> AgentPerformance(CancellationToken ct)
    {
        var agents = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role.Name == "Agent")
            .Select(u => new
            {
                u.Id,
                u.EmployeeCode,
                u.FullName,
                team = u.Team == null ? null : u.Team.Name,
                maxConcurrentTickets = u.MaxConcurrentTickets,
                assigned = db.Tickets.Count(t => t.AssignedToId == u.Id),
                open = db.Tickets.Count(t => t.AssignedToId == u.Id &&
                    t.Status != TicketStatus.Closed &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Cancelled),
                resolved = db.Tickets.Count(t => t.AssignedToId == u.Id &&
                    (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed)),
                slaBreached = db.Tickets.Count(t => t.AssignedToId == u.Id && t.IsSlaBreached)
            })
            .OrderByDescending(x => x.resolved)
            .ThenBy(x => x.open)
            .ToListAsync(ct);

        return Ok(agents.Select(x => new
        {
            x.Id,
            x.EmployeeCode,
            x.FullName,
            x.team,
            x.maxConcurrentTickets,
            x.assigned,
            x.open,
            x.resolved,
            x.slaBreached,
            resolutionRate = x.assigned == 0 ? 0 : Math.Round(x.resolved * 100.0 / x.assigned, 2),
            loadPercent = x.maxConcurrentTickets == 0 ? 0 : Math.Round(x.open * 100.0 / x.maxConcurrentTickets, 2)
        }));
    }

    [Authorize(Roles = "Admin,Supervisor")]
    [HttpGet("trends")]
    public async Task<IActionResult> Trends([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 90);
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= from)
            .Select(t => new { t.CreatedAt, t.ResolvedAt, t.Status })
            .ToListAsync(ct);

        var result = Enumerable.Range(0, days).Select(i =>
        {
            var date = from.AddDays(i).Date;
            var day = tickets.Where(t => t.CreatedAt.Date == date).ToList();
            var resolved = tickets.Count(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value.Date == date);
            return new
            {
                date = date.ToString("yyyy-MM-dd"),
                created = day.Count,
                resolved,
                open = day.Count(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Cancelled)
            };
        });

        return Ok(result);
    }

    [Authorize(Roles = "Admin,Supervisor")]
    [HttpGet("sla")]
    public async Task<IActionResult> Sla(CancellationToken ct)
    {
        var total = await db.Tickets.CountAsync(ct);
        var breached = await db.Tickets.CountAsync(t => t.IsSlaBreached, ct);
        var resolved = await db.Tickets.CountAsync(t => t.ResolvedAt != null, ct);
        var onTimeResolved = await db.Tickets.CountAsync(t => t.ResolvedAt != null && !t.IsSlaBreached, ct);

        return Ok(new
        {
            total,
            breached,
            resolved,
            onTimeResolved,
            complianceRate = total == 0 ? 0 : Math.Round((total - breached) * 100.0 / total, 2),
            resolvedOnTimeRate = resolved == 0 ? 0 : Math.Round(onTimeResolved * 100.0 / resolved, 2)
        });
    }

}
