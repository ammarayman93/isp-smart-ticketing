namespace ISP.Ticketing.Application.DTOs.Tickets;

public class TicketDetailsDto
{
    public long Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerRegion { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Priority { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Source { get; set; } = null!;
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? SlaDueAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public decimal? AiConfidence { get; set; }
}