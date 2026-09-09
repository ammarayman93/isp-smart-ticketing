namespace ISP.Ticketing.Domain.Entities;

public class TicketComment
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public int UserId { get; set; }
    public string Comment { get; set; } = null!;
    public bool IsInternal { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User User { get; set; } = null!;
}