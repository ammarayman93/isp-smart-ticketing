using ISP.Ticketing.Domain.Common;

namespace ISP.Ticketing.Domain.Entities;

public class User : BaseEntity
{
    public string EmployeeCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Phone { get; set; }
    public int RoleId { get; set; }
    public int? TeamId { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxConcurrentTickets { get; set; } = 10;

    public Role Role { get; set; } = null!;
    public Team? Team { get; set; }
}