using ISP.Ticketing.Domain.Common;

namespace ISP.Ticketing.Domain.Entities;

public class Team : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;

    public User? Manager { get; set; }
    public ICollection<User> Members { get; set; } = new List<User>();
}