using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class RoutingService(ApplicationDbContext db) : IRoutingService
{
    public async Task<Result<RoutingResult>> RouteAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var category = ticket.CategoryId.HasValue
            ? await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ticket.CategoryId.Value, cancellationToken)
            : null;

        var preferredNames = category?.Name switch
        {
            "Service Outage" => new[] { "Network Operations", "Technical Support" },
            "Slow Speed" => new[] { "Technical Support", "Network Operations" },
            "Router Issues" => new[] { "Technical Support" },
            "WiFi Problems" => new[] { "Technical Support" },
            "Billing" => new[] { "Billing" },
            _ => Array.Empty<string>()
        };

        var teams = await db.Teams.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);
        if (teams.Count == 0)
            return Result<RoutingResult>.Failure("No active support team is configured.");

        var team = preferredNames.Length > 0
            ? teams.FirstOrDefault(t => preferredNames.Contains(t.Name))
            : null;
        team ??= teams.OrderBy(t => t.Id).First();

        var activeStatuses = new[] { TicketStatus.New, TicketStatus.Classified, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.PendingCustomer };
        var agents = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.TeamId == team.Id)
            .Select(u => new
            {
                User = u,
                Load = db.Tickets.Count(t => t.AssignedToId == u.Id && activeStatuses.Contains(t.Status))
            })
            .Where(x => x.Load < x.User.MaxConcurrentTickets)
            .OrderBy(x => x.Load)
            .ThenBy(x => x.User.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<RoutingResult>.Success(new RoutingResult
        {
            TeamId = team.Id,
            TeamName = team.Name,
            AgentId = agents?.User.Id,
            AgentName = agents?.User.FullName,
            RoutingReason = category is null
                ? "Fallback routing to the first active team."
                : $"Category-based routing to {team.Name}. Agent selected by lowest active workload."
        });
    }
}
