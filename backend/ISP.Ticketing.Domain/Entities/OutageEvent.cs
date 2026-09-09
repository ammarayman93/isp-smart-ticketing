using ISP.Ticketing.Domain.Enums;

namespace ISP.Ticketing.Domain.Entities;

public class OutageEvent
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Region { get; set; }
    public int? AffectedCategoryId { get; set; }
    public OutageSeverity Severity { get; set; } = OutageSeverity.Medium;
    public OutageStatus Status { get; set; } = OutageStatus.Detected;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public int EstimatedAffectedCustomers { get; set; } = 0;
    public string? RootCause { get; set; }
    public bool CreatedBySystem { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? AffectedCategory { get; set; }
    public ICollection<Ticket> AffectedTickets { get; set; } = new List<Ticket>();
}