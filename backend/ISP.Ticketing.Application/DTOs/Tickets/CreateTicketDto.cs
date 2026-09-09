namespace ISP.Ticketing.Application.DTOs.Tickets;

public class CreateTicketDto
{
    public string CustomerId { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerRegion { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Source { get; set; } = "Phone";
}