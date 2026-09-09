using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class OutageDetectionService(ApplicationDbContext db) : IOutageDetectionService
{
    public async Task<Result<OutageEvent?>> DetectForTicketAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.CustomerRegion))
            return Result<OutageEvent?>.Success(null);

        var since = DateTime.UtcNow.AddMinutes(-15);
        var query = db.Tickets.Where(x => x.CreatedAt >= since && x.CustomerRegion == ticket.CustomerRegion);
        if (ticket.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == ticket.CategoryId);

        var count = await query.CountAsync(cancellationToken);
        if (count < 3)
            return Result<OutageEvent?>.Success(null);

        var outage = await db.OutageEvents.FirstOrDefaultAsync(x =>
            x.Status != OutageStatus.Resolved && x.Status != OutageStatus.Closed &&
            x.Region == ticket.CustomerRegion && x.AffectedCategoryId == ticket.CategoryId, cancellationToken);

        if (outage is null)
        {
            outage = new OutageEvent
            {
                Title = $"Potential mass outage - {ticket.CustomerRegion}",
                Description = $"{count} related tickets detected within 15 minutes.",
                Region = ticket.CustomerRegion,
                AffectedCategoryId = ticket.CategoryId,
                Severity = count >= 10 ? OutageSeverity.Critical : count >= 5 ? OutageSeverity.High : OutageSeverity.Medium,
                EstimatedAffectedCustomers = count,
                CreatedBySystem = true,
                DetectedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.OutageEvents.Add(outage);
        }
        else
        {
            outage.EstimatedAffectedCustomers = Math.Max(outage.EstimatedAffectedCustomers, count);
            outage.UpdatedAt = DateTime.UtcNow;
        }

        ticket.OutageEvent = outage;
        await db.SaveChangesAsync(cancellationToken);
        return Result<OutageEvent?>.Success(outage);
    }
}
