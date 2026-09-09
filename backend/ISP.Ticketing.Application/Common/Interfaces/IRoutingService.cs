using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Entities;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface IRoutingService
{
    Task<Result<RoutingResult>> RouteAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}

public class RoutingResult
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = null!;
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
    public string RoutingReason { get; set; } = null!;
}