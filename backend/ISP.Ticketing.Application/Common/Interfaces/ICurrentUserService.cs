namespace ISP.Ticketing.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}