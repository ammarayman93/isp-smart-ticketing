using ISP.Ticketing.Infrastructure.Identity;
using ISP.Ticketing.Infrastructure.Persistence;
using ISP.Ticketing.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ApplicationDbContext context, IJwtTokenService jwt) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var user = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);
        if (user is null) return Unauthorized(new { message = "Invalid credentials." });

        var valid = PasswordService.Verify(request.Password, user.PasswordHash);
        if (!valid && user.PasswordHash == request.Password)
        {
            user.PasswordHash = PasswordService.Hash(request.Password);
            await context.SaveChangesAsync(ct);
            valid = true;
        }
        if (!valid) return Unauthorized(new { message = "Invalid credentials." });

        return Ok(new
        {
            token = jwt.GenerateToken(user),
            user = new { user.Id, user.EmployeeCode, user.FullName, user.Email, user.TeamId, role = user.Role?.Name }
        });
    }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
