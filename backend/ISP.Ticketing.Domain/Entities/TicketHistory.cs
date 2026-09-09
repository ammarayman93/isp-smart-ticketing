namespace ISP.Ticketing.Domain.Entities;

public class TicketHistory
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string Action { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public int? ChangedById { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User? ChangedBy { get; set; }
}