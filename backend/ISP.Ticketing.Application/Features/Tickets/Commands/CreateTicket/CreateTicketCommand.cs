using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Application.DTOs.Tickets;
using MediatR;

namespace ISP.Ticketing.Application.Features.Tickets.Commands.CreateTicket;

public record CreateTicketCommand(CreateTicketDto Ticket) : IRequest<Result<TicketDetailsDto>>;