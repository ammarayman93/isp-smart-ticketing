using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Entities;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface IOutageDetectionService
{
    Task<Result<OutageEvent?>> DetectForTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
