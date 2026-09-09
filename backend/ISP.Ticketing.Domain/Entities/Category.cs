using ISP.Ticketing.Domain.Common;
using ISP.Ticketing.Domain.Enums;

namespace ISP.Ticketing.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;
    public int SlaHours { get; set; } = 24;
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}