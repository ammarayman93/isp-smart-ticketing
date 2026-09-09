using ISP.Ticketing.Domain.Enums;

namespace ISP.Ticketing.Domain.Entities;

public class Ticket
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
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketSource Source { get; set; } = TicketSource.Phone;

    public int? AssignedToId { get; set; }
    public int? TeamId { get; set; }
    public long? OutageEventId { get; set; }
    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClassifiedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? SlaDueAt { get; set; }
    public bool IsSlaBreached { get; set; } = false;

    public decimal? SentimentScore { get; set; }
    public decimal? AiConfidence { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public Category? Category { get; set; }
    public User? AssignedTo { get; set; }
    public Team? Team { get; set; }
    public User CreatedBy { get; set; } = null!;
    public OutageEvent? OutageEvent { get; set; }

    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}