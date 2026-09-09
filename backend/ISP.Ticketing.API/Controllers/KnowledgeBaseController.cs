using System.Text.Json;
using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KnowledgeBaseController(ApplicationDbContext db, IKnowledgeBaseService knowledgeBase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var rows = await db.KnowledgeArticles.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Id).ToListAsync(ct);
        return Ok(rows.Select(x => new { x.Id, x.Title, x.TitleAr, x.Category, x.Summary, x.Solution, steps = JsonSerializer.Deserialize<string[]>(x.StepsJson) ?? Array.Empty<string>(), x.IsActive, x.CreatedAt, x.UpdatedAt }));
    }
    [HttpGet("recommend")]
    public async Task<IActionResult> Recommend([FromQuery] string title, [FromQuery] string description, [FromQuery] int? categoryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description)) return BadRequest(new { message = "Title or description is required." });
        var result = await knowledgeBase.RecommendAsync(title ?? "", description ?? "", categoryId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }
    [Authorize(Roles = "Admin,Supervisor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] KnowledgeArticleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.TitleAr) || string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Solution)) return BadRequest(new { message = "Title, Arabic title, category and solution are required." });
        var article = new KnowledgeArticle { Title = request.Title.Trim(), TitleAr = request.TitleAr.Trim(), Category = request.Category.Trim(), Summary = request.Summary?.Trim() ?? "", Solution = request.Solution.Trim(), StepsJson = JsonSerializer.Serialize(request.Steps ?? Array.Empty<string>()), IsActive = request.IsActive, CreatedAt = DateTime.UtcNow };
        db.KnowledgeArticles.Add(article); await db.SaveChangesAsync(ct); return Ok(article);
    }
    [Authorize(Roles = "Admin,Supervisor")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] KnowledgeArticleRequest request, CancellationToken ct)
    {
        var article = await db.KnowledgeArticles.FirstOrDefaultAsync(x => x.Id == id, ct); if (article is null) return NotFound(new { message = "Knowledge article not found." });
        article.Title = request.Title.Trim(); article.TitleAr = request.TitleAr.Trim(); article.Category = request.Category.Trim(); article.Summary = request.Summary?.Trim() ?? ""; article.Solution = request.Solution.Trim(); article.StepsJson = JsonSerializer.Serialize(request.Steps ?? Array.Empty<string>()); article.IsActive = request.IsActive; article.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(article);
    }
    [Authorize(Roles = "Admin,Supervisor")]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> Status(int id, [FromBody] KnowledgeStatusRequest request, CancellationToken ct)
    {
        var article = await db.KnowledgeArticles.FirstOrDefaultAsync(x => x.Id == id, ct); if (article is null) return NotFound(new { message = "Knowledge article not found." });
        article.IsActive = request.IsActive; article.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new { article.Id, article.IsActive });
    }
}
public sealed class KnowledgeArticleRequest { public string Title { get; set; } = ""; public string TitleAr { get; set; } = ""; public string Category { get; set; } = ""; public string? Summary { get; set; } public string Solution { get; set; } = ""; public string[]? Steps { get; set; } public bool IsActive { get; set; } = true; }
public sealed class KnowledgeStatusRequest { public bool IsActive { get; set; } }
