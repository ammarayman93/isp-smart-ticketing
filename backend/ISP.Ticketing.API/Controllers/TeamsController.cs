using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var teams = await context.Teams.AsNoTracking()
            .Include(x => x.Manager)
            .Include(x => x.Members)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.IsActive,
                x.ManagerId,
                Manager = x.Manager == null ? null : x.Manager.FullName,
                MemberCount = x.Members.Count(u => u.IsActive)
            })
            .ToListAsync(ct);

        return Ok(teams);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var team = await context.Teams.AsNoTracking()
            .Include(x => x.Manager)
            .Include(x => x.Members).ThenInclude(x => x.Role)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.IsActive,
                x.ManagerId,
                Manager = x.Manager == null ? null : x.Manager.FullName,
                members = x.Members
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new
                    {
                        u.Id,
                        u.EmployeeCode,
                        u.FullName,
                        u.Email,
                        Role = u.Role.Name,
                        u.MaxConcurrentTickets
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        return team is null
            ? NotFound(new { message = "Team not found." })
            : Ok(team);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Team name is required." });

        var name = request.Name.Trim();
        if (await context.Teams.AnyAsync(x => x.Name == name, ct))
            return Conflict(new { message = "A team with this name already exists." });

        if (request.ManagerId.HasValue &&
            !await context.Users.AnyAsync(x => x.Id == request.ManagerId.Value && x.IsActive, ct))
            return BadRequest(new { message = "Selected manager does not exist or is inactive." });

        var team = new Team
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ManagerId = request.ManagerId,
            IsActive = true
        };

        context.Teams.Add(team);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
    {
        var team = await context.Teams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (team is null)
            return NotFound(new { message = "Team not found." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Team name is required." });

        var name = request.Name.Trim();
        if (await context.Teams.AnyAsync(x => x.Id != id && x.Name == name, ct))
            return Conflict(new { message = "A team with this name already exists." });

        if (request.ManagerId.HasValue &&
            !await context.Users.AnyAsync(x => x.Id == request.ManagerId.Value && x.IsActive, ct))
            return BadRequest(new { message = "Selected manager does not exist or is inactive." });

        team.Name = name;
        team.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        team.ManagerId = request.ManagerId;
        team.IsActive = request.IsActive;
        team.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return Ok(new { message = "Team updated successfully." });
    }
}

public sealed class CreateTeamRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? ManagerId { get; set; }
}

public sealed class UpdateTeamRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
