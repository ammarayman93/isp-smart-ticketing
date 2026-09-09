using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Infrastructure.Persistence;
using ISP.Ticketing.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public class UsersController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var users = await context.Users.AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Team)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.EmployeeCode,
                x.FullName,
                x.Email,
                x.Phone,
                x.IsActive,
                x.MaxConcurrentTickets,
                x.RoleId,
                Role = x.Role.Name,
                x.TeamId,
                Team = x.Team == null ? null : x.Team.Name,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var user = await context.Users.AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Team)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.EmployeeCode,
                x.FullName,
                x.Email,
                x.Phone,
                x.IsActive,
                x.MaxConcurrentTickets,
                x.RoleId,
                Role = x.Role.Name,
                x.TeamId,
                Team = x.Team == null ? null : x.Team.Name,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        return user is null
            ? NotFound(new { message = "User not found." })
            : Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) ||
            string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Role))
            return BadRequest(new { message = "EmployeeCode, FullName, Email, Password and Role are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(x => x.Email == email, ct))
            return Conflict(new { message = "A user with this email already exists." });

        var role = await context.Roles.FirstOrDefaultAsync(
            x => x.Name == request.Role.Trim(), ct);
        if (role is null)
            return BadRequest(new { message = "Invalid role." });

        Team? team = null;
        if (request.TeamId.HasValue)
        {
            team = await context.Teams.FirstOrDefaultAsync(
                x => x.Id == request.TeamId.Value && x.IsActive, ct);
            if (team is null)
                return BadRequest(new { message = "Selected team does not exist or is inactive." });
        }

        if (role.Name == "Agent" && team is null)
            return BadRequest(new { message = "An Agent must be assigned to a team." });

        var user = new User
        {
            EmployeeCode = request.EmployeeCode.Trim(),
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordService.Hash(request.Password),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = role.Id,
            TeamId = team?.Id,
            IsActive = request.IsActive,
            MaxConcurrentTickets = Math.Clamp(request.MaxConcurrentTickets, 1, 100)
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
        {
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Email,
            role = role.Name,
            teamId = user.TeamId
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
            return NotFound(new { message = "User not found." });

        var role = await context.Roles.FirstOrDefaultAsync(
            x => x.Name == request.Role.Trim(), ct);
        if (role is null)
            return BadRequest(new { message = "Invalid role." });

        Team? team = null;
        if (request.TeamId.HasValue)
        {
            team = await context.Teams.FirstOrDefaultAsync(
                x => x.Id == request.TeamId.Value && x.IsActive, ct);
            if (team is null)
                return BadRequest(new { message = "Selected team does not exist or is inactive." });
        }

        if (role.Name == "Agent" && team is null)
            return BadRequest(new { message = "An Agent must be assigned to a team." });

        user.EmployeeCode = request.EmployeeCode.Trim();
        user.FullName = request.FullName.Trim();
        user.Email = request.Email.Trim().ToLowerInvariant();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.RoleId = role.Id;
        user.TeamId = team?.Id;
        user.IsActive = request.IsActive;
        user.MaxConcurrentTickets = Math.Clamp(request.MaxConcurrentTickets, 1, 100);
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = PasswordService.Hash(request.Password);

        await context.SaveChangesAsync(ct);
        return Ok(new { message = "User updated successfully." });
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeUserStatusRequest request, CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
            return NotFound(new { message = "User not found." });

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.IsActive });
    }
}

public sealed class CreateUserRequest
{
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "Agent";
    public int? TeamId { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxConcurrentTickets { get; set; } = 10;
}

public sealed class UpdateUserRequest
{
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Password { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "Agent";
    public int? TeamId { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxConcurrentTickets { get; set; } = 10;
}

public sealed class ChangeUserStatusRequest
{
    public bool IsActive { get; set; }
}
