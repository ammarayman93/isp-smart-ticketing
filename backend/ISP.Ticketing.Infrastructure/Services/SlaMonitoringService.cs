using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class SlaMonitoringService(IServiceScopeFactory scopes, ILogger<SlaMonitoringService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var tickets = await db.Tickets.Where(t => !t.IsSlaBreached && t.SlaDueAt.HasValue && t.SlaDueAt.Value < now && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled).ToListAsync(stoppingToken);
                if (tickets.Count > 0)
                {
                    foreach (var ticket in tickets) ticket.IsSlaBreached = true;
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogWarning("Marked {Count} tickets as SLA breached", tickets.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "SLA monitoring failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
