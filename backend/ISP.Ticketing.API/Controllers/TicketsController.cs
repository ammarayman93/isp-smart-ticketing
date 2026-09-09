using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Domain.Enums;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController(
    ApplicationDbContext context,
    ICurrentUserService currentUser,
    IClassificationService classifier,
    IRoutingService router,
    IOutageDetectionService outageDetector, IKnowledgeBaseService knowledgeBase, IWebHostEnvironment environment) : ControllerBase
{
    private async Task<IQueryable<Ticket>> GetAccessibleTicketsAsync(CancellationToken ct)
    {
        var query = context.Tickets.AsQueryable();

        if (User.IsInRole("Admin"))
            return query;

        if (User.IsInRole("Supervisor"))
        {
            var teamId = await context.Users
                .Where(x => x.Id == currentUser.UserId)
                .Select(x => x.TeamId)
                .FirstOrDefaultAsync(ct);

            return teamId.HasValue
                ? query.Where(x => x.TeamId == teamId.Value)
                : query.Where(x => x.CreatedById == currentUser.UserId);
        }

        return query.Where(x => x.AssignedToId == currentUser.UserId || x.CreatedById == currentUser.UserId);
    }

    private async Task<bool> CanAccessTicketAsync(Ticket ticket, CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (User.IsInRole("Supervisor"))
        {
            var teamId = await context.Users
                .Where(x => x.Id == currentUser.UserId)
                .Select(x => x.TeamId)
                .FirstOrDefaultAsync(ct);
            return teamId.HasValue && ticket.TeamId == teamId.Value;
        }

        return ticket.AssignedToId == currentUser.UserId || ticket.CreatedById == currentUser.UserId;
    }

    private static bool IsTransitionAllowed(TicketStatus oldStatus, TicketStatus newStatus)
    {
        if (oldStatus == newStatus)
            return true;

        return oldStatus switch
        {
            TicketStatus.New => newStatus == TicketStatus.Classified || newStatus == TicketStatus.Cancelled,
            TicketStatus.Classified => newStatus == TicketStatus.Assigned || newStatus == TicketStatus.Cancelled,
            TicketStatus.Assigned => newStatus == TicketStatus.InProgress || newStatus == TicketStatus.Cancelled,
            TicketStatus.InProgress => newStatus == TicketStatus.PendingCustomer || newStatus == TicketStatus.Resolved || newStatus == TicketStatus.Cancelled,
            TicketStatus.PendingCustomer => newStatus == TicketStatus.InProgress || newStatus == TicketStatus.Resolved || newStatus == TicketStatus.Cancelled,
            TicketStatus.Resolved => newStatus == TicketStatus.Closed,
            TicketStatus.Closed => false,
            TicketStatus.Cancelled => false,
            _ => false
        };
    }

    private async Task<User?> GetCurrentUserAsync(CancellationToken ct)
    {
        return await context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == currentUser.UserId, ct);
    }

    private async Task<bool> CanManageAssignmentAsync(Ticket ticket, CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (!User.IsInRole("Supervisor"))
            return false;

        var user = await GetCurrentUserAsync(ct);
        return user?.TeamId.HasValue == true && ticket.TeamId == user.TeamId.Value;
    }

    private async Task<bool> CanAssignToUserAsync(User actor, User assignee, int teamId, CancellationToken ct)
    {
        if (!assignee.IsActive || assignee.Role.Name != "Agent")
            return false;

        if (assignee.TeamId != teamId)
            return false;

        if (actor.Role.Name == "Supervisor" && actor.TeamId != teamId)
            return false;

        // Do not assign beyond the agent's configured workload limit.
        var activeStatuses = new[]
        {
            TicketStatus.Assigned,
            TicketStatus.InProgress,
            TicketStatus.PendingCustomer
        };

        var activeCount = await context.Tickets.CountAsync(
            x => x.AssignedToId == assignee.Id && activeStatuses.Contains(x.Status), ct);

        return activeCount < assignee.MaxConcurrentTickets;
    }

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> GetHistory(long id, CancellationToken ct)
    {
        var ticket = await context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (ticket is null)
            return NotFound(new { message = "Ticket not found." });

        if (!await CanAccessTicketAsync(ticket, ct))
            return Forbid();

        var history = await context.TicketHistories
            .AsNoTracking()
            .Where(x => x.TicketId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Action,
                x.OldValue,
                x.NewValue,
                x.Notes,
                x.CreatedAt,
                x.ChangedById,
                ChangedBy = x.ChangedBy == null ? null : x.ChangedBy.FullName
            })
            .ToListAsync(ct);

        return Ok(history);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? priority,
        [FromQuery] string? category, [FromQuery] int? teamId, [FromQuery] int? assignedToId,
        [FromQuery] string? region, [FromQuery] bool? slaBreached, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 5, 100);
        var q = await GetAccessibleTicketsAsync(ct);
        q = q.AsNoTracking().Include(t => t.Category).Include(t => t.Team).Include(t => t.AssignedTo);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(t => t.TicketNumber.Contains(s) || t.CustomerId.Contains(s) || t.CustomerName.Contains(s) || t.Title.Contains(s) || t.Description.Contains(s)); }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, true, out var st)) q = q.Where(t => t.Status == st);
        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TicketPriority>(priority, true, out var pr)) q = q.Where(t => t.Priority == pr);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => t.Category != null && t.Category.Name == category);
        if (teamId.HasValue) q = q.Where(t => t.TeamId == teamId);
        if (assignedToId.HasValue) q = q.Where(t => t.AssignedToId == assignedToId);
        if (!string.IsNullOrWhiteSpace(region)) q = q.Where(t => t.CustomerRegion == region);
        if (slaBreached.HasValue) q = q.Where(t => t.IsSlaBreached == slaBreached.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(t => new {
            t.Id, t.TicketNumber, t.CustomerId, t.CustomerName, t.Title, t.Description,
            Category = t.Category == null ? null : t.Category.Name, Priority = t.Priority.ToString(),
            Status = t.Status.ToString(), Source = t.Source.ToString(), t.CustomerRegion, t.CreatedAt,
            t.ClassifiedAt, t.AssignedAt, t.FirstResponseAt, t.ResolvedAt, t.ClosedAt, t.SlaDueAt, t.IsSlaBreached,
            t.AssignedToId, AssignedTo = t.AssignedTo == null ? null : t.AssignedTo.FullName,
            t.TeamId, Team = t.Team == null ? null : t.Team.Name, t.AiConfidence, t.OutageEventId
        }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var t = await context.Tickets.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Team).Include(x => x.AssignedTo)
            .Include(x => x.History).Include(x => x.Comments).ThenInclude(c => c.User).Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(t, ct)) return Forbid();

        return Ok(new
        {
            t.Id, t.TicketNumber, t.CustomerId, t.CustomerName, t.CustomerPhone, t.CustomerRegion,
            t.Title, t.Description, category = t.Category == null ? null : new { t.Category.Id, t.Category.Name, t.Category.NameAr },
            priority = t.Priority.ToString(), status = t.Status.ToString(), source = t.Source.ToString(),
            t.CreatedAt, t.ClassifiedAt, t.AssignedAt, t.FirstResponseAt, t.ResolvedAt, t.ClosedAt,
            t.SlaDueAt, t.IsSlaBreached, t.AiConfidence, t.SentimentScore, t.TeamId,
            team = t.Team == null ? null : new { t.Team.Id, t.Team.Name },
            assignedTo = t.AssignedTo == null ? null : new { t.AssignedTo.Id, t.AssignedTo.FullName, t.AssignedTo.Email },
            t.OutageEventId,
            history = t.History.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.Action, x.OldValue, x.NewValue, x.Notes, x.CreatedAt, x.ChangedById }).ToList(),
            comments = t.Comments.OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.Comment, x.IsInternal, x.CreatedAt, x.UserId, user = x.User == null ? null : x.User.FullName }).ToList(),
            attachments = t.Attachments.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.FileName, x.FileType, x.FileSize, x.CreatedAt, x.UploadedById }).ToList()
        });
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId) || string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Customer, title and description are required." });

        var source = Enum.TryParse<TicketSource>(request.Source, true, out var parsedSource) ? parsedSource : TicketSource.Phone;
        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            TicketNumber = $"TKT-{now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            CustomerId = request.CustomerId.Trim(), CustomerName = request.CustomerName.Trim(), CustomerPhone = request.CustomerPhone,
            CustomerRegion = request.CustomerRegion?.Trim(), Title = request.Title.Trim(), Description = request.Description.Trim(),
            Source = source, Status = TicketStatus.New, Priority = TicketPriority.Medium,
            CreatedById = currentUser.UserId, CreatedAt = now
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(ct);

        var classification = await classifier.ClassifyAsync(ticket.Title, ticket.Description, ct);
        if (!classification.IsSuccess || classification.Value is null) return Ok(new { ticket.Id, ticket.TicketNumber, message = "Ticket created; automatic classification unavailable." });
        var c = classification.Value;
        ticket.CategoryId = c.CategoryId; ticket.Priority = c.Priority; ticket.AiConfidence = c.Confidence;
        ticket.ClassifiedAt = DateTime.UtcNow; ticket.Status = TicketStatus.Classified; ticket.SlaDueAt = now.AddHours(c.SlaHours);
        context.ClassificationLogs.Add(new ClassificationLog
        {
            TicketId = ticket.Id, PredictedCategoryId = c.CategoryId, PredictedPriority = c.Priority.ToString(),
            ConfidenceScore = c.Confidence, ModelVersion = c.ModelVersion, ProcessingTimeMs = c.ProcessingTimeMs
        });
        context.TicketHistories.Add(new TicketHistory { TicketId = ticket.Id, Action = "Classified", NewValue = c.CategoryName, ChangedById = currentUser.UserId, Notes = $"ML.NET confidence={c.Confidence:P1}" });
        await context.SaveChangesAsync(ct);

        var route = await router.RouteAsync(ticket, ct);
        if (route.IsSuccess && route.Value is not null)
        {
            ticket.TeamId = route.Value.TeamId; ticket.AssignedToId = route.Value.AgentId; ticket.AssignedAt = DateTime.UtcNow; ticket.Status = TicketStatus.Assigned;
            context.TicketHistories.Add(new TicketHistory { TicketId = ticket.Id, Action = "Assigned", NewValue = route.Value.TeamName, ChangedById = currentUser.UserId, Notes = route.Value.RoutingReason });
        }
        await context.SaveChangesAsync(ct);

        var outage = await outageDetector.DetectForTicketAsync(ticket, ct);
        if (outage.IsSuccess && outage.Value is not null)
        {
            ticket.OutageEventId = outage.Value.Id;
            context.TicketHistories.Add(new TicketHistory { TicketId = ticket.Id, Action = "OutageDetected", NewValue = outage.Value.Title, ChangedById = currentUser.UserId });
            await context.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, new
        {
            ticket.Id, ticket.TicketNumber, category = c.CategoryName, priority = ticket.Priority.ToString(),
            status = ticket.Status.ToString(), teamId = ticket.TeamId, assignedToId = ticket.AssignedToId,
            aiConfidence = ticket.AiConfidence, slaDueAt = ticket.SlaDueAt, outageEventId = ticket.OutageEventId,
            recommendations = (await knowledgeBase.RecommendAsync(ticket.Title, ticket.Description, ticket.CategoryId, ct)).Value ?? Array.Empty<KnowledgeArticleResult>()
        });
    }
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<TicketStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Invalid status." });

        var ticket = await context.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null)
            return NotFound(new { message = "Ticket not found." });

        if (!await CanAccessTicketAsync(ticket, ct))
            return Forbid();

        var old = ticket.Status;
        if (!IsTransitionAllowed(old, status))
        {
            return BadRequest(new
            {
                message = $"Invalid status transition: {old} -> {status}.",
                currentStatus = old.ToString(),
                requestedStatus = status.ToString()
            });
        }

        // Agents may only work through the operational states of tickets assigned to them.
        if (User.IsInRole("Agent") && ticket.AssignedToId != currentUser.UserId)
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        ticket.Status = status;
        ticket.UpdatedAt = now;

        if (status == TicketStatus.InProgress && ticket.FirstResponseAt is null)
            ticket.FirstResponseAt = now;

        if (status == TicketStatus.Resolved)
            ticket.ResolvedAt = now;

        if (status == TicketStatus.Closed)
            ticket.ClosedAt = now;

        if (status == TicketStatus.Cancelled)
        {
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
        }

        context.TicketHistories.Add(new TicketHistory
        {
            TicketId = id,
            Action = "StatusChanged",
            OldValue = old.ToString(),
            NewValue = status.ToString(),
            ChangedById = currentUser.UserId,
            Notes = request.Notes
        });

        await context.SaveChangesAsync(ct);

        return Ok(new
        {
            ticket.Id,
            ticket.TicketNumber,
            oldStatus = old.ToString(),
            status = ticket.Status.ToString(),
            ticket.FirstResponseAt,
            ticket.ResolvedAt,
            ticket.ClosedAt
        });
    }

    [HttpPatch("{id:long}/assignment")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Assign(
        long id,
        [FromBody] AssignTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await context.Tickets
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (ticket is null)
            return NotFound(new { message = "Ticket not found." });

        if (!await CanManageAssignmentAsync(ticket, ct))
            return Forbid();

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            return BadRequest(new { message = "Closed or cancelled tickets cannot be assigned." });

        if (request.TeamId <= 0 || request.AssignedToId <= 0)
            return BadRequest(new { message = "TeamId and AssignedToId are required." });

        var team = await context.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, ct);

        if (team is null || !team.IsActive)
            return BadRequest(new { message = "The selected team does not exist or is inactive." });

        var assignee = await context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == request.AssignedToId, ct);

        if (assignee is null || !assignee.IsActive)
            return BadRequest(new { message = "The selected agent does not exist or is inactive." });

        if (assignee.Role.Name != "Agent")
            return BadRequest(new { message = "Tickets can only be assigned to Agent users." });

        if (assignee.TeamId != team.Id)
            return BadRequest(new { message = "The selected agent does not belong to the selected team." });

        var actor = await GetCurrentUserAsync(ct);
        if (actor is null)
            return Unauthorized();

        if (actor.Role.Name == "Supervisor" && actor.TeamId != team.Id)
            return Forbid();

        var oldTeamId = ticket.TeamId;
        var oldAssigneeId = ticket.AssignedToId;

        // When the same agent is already assigned, do not count this ticket against the limit again.
        if (oldAssigneeId != assignee.Id)
        {
            var canAssign = await CanAssignToUserAsync(actor, assignee, team.Id, ct);
            if (!canAssign)
            {
                return Conflict(new
                {
                    message = "The selected agent has reached the maximum number of concurrent tickets or cannot receive this ticket."
                });
            }
        }

        var now = DateTime.UtcNow;
        ticket.TeamId = team.Id;
        ticket.AssignedToId = assignee.Id;
        ticket.AssignedAt = now;
        ticket.UpdatedAt = now;

        if (ticket.Status is TicketStatus.New or TicketStatus.Classified)
            ticket.Status = TicketStatus.Assigned;

        var assignmentNotes = string.IsNullOrWhiteSpace(request.Notes)
            ? "Ticket assignment updated."
            : request.Notes.Trim();

        if (oldTeamId != team.Id)
        {
            context.TicketHistories.Add(new TicketHistory
            {
                TicketId = id,
                Action = "TeamChanged",
                OldValue = oldTeamId?.ToString(),
                NewValue = team.Id.ToString(),
                ChangedById = currentUser.UserId,
                Notes = $"Team: {team.Name}. {assignmentNotes}"
            });
        }

        if (oldAssigneeId != assignee.Id)
        {
            context.TicketHistories.Add(new TicketHistory
            {
                TicketId = id,
                Action = "Assigned",
                OldValue = oldAssigneeId?.ToString(),
                NewValue = assignee.Id.ToString(),
                ChangedById = currentUser.UserId,
                Notes = $"Assigned to {assignee.FullName}. {assignmentNotes}"
            });
        }
        else if (oldTeamId == team.Id)
        {
            context.TicketHistories.Add(new TicketHistory
            {
                TicketId = id,
                Action = "AssignmentUpdated",
                OldValue = assignee.Id.ToString(),
                NewValue = assignee.Id.ToString(),
                ChangedById = currentUser.UserId,
                Notes = assignmentNotes
            });
        }

        await context.SaveChangesAsync(ct);

        return Ok(new
        {
            ticket.Id,
            ticket.TicketNumber,
            status = ticket.Status.ToString(),
            teamId = ticket.TeamId,
            team = team.Name,
            assignedToId = ticket.AssignedToId,
            assignedTo = assignee.FullName,
            ticket.AssignedAt
        });
    }

    [HttpGet("{id:long}/recommendations")]
    public async Task<IActionResult> Recommendations(long id, CancellationToken ct)
    {
        var ticket = await context.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(ticket, ct)) return Forbid();
        var result = await knowledgeBase.RecommendAsync(ticket.Title, ticket.Description, ticket.CategoryId, ct);
        return Ok(result.Value ?? Array.Empty<KnowledgeArticleResult>());
    }

    [HttpPost("{id:long}/comments")]
    public async Task<IActionResult> AddComment(long id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Comment)) return BadRequest(new { message = "Comment is required." });
        var ticket = await context.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(ticket, ct)) return Forbid();
        var comment = new TicketComment { TicketId = id, UserId = currentUser.UserId, Comment = request.Comment.Trim(), IsInternal = request.IsInternal };
        context.TicketComments.Add(comment);
        context.TicketHistories.Add(new TicketHistory { TicketId = id, Action = "CommentAdded", ChangedById = currentUser.UserId, Notes = request.IsInternal ? "Internal comment" : "Customer-visible comment" });
        await context.SaveChangesAsync(ct);
        return Ok(comment);
    }

    [HttpPost("{id:long}/attachments")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadAttachment(long id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "File is required." });
        var ticket = await context.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(ticket, ct)) return Forbid();
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".pdf", ".txt", ".log", ".docx", ".xlsx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return BadRequest(new { message = "File type is not allowed." });
        var root = Path.Combine(environment.ContentRootPath, "uploads", "tickets", id.ToString());
        Directory.CreateDirectory(root);
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(root, safeName);
        await using (var stream = System.IO.File.Create(path)) await file.CopyToAsync(stream, ct);
        var attachment = new TicketAttachment { TicketId = id, FileName = Path.GetFileName(file.FileName), FilePath = path, FileType = file.ContentType, FileSize = checked((int)file.Length), UploadedById = currentUser.UserId, CreatedAt = DateTime.UtcNow };
        context.TicketAttachments.Add(attachment);
        context.TicketHistories.Add(new TicketHistory { TicketId = id, Action = "AttachmentAdded", NewValue = attachment.FileName, ChangedById = currentUser.UserId });
        await context.SaveChangesAsync(ct);
        return Ok(new { attachment.Id, attachment.FileName, attachment.FileType, attachment.FileSize, attachment.CreatedAt });
    }

    [HttpGet("{id:long}/attachments")]
    public async Task<IActionResult> GetAttachments(long id, CancellationToken ct)
    {
        var ticket = await context.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(ticket, ct)) return Forbid();
        return Ok(await context.TicketAttachments.AsNoTracking().Where(x => x.TicketId == id).OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.FileName, x.FileType, x.FileSize, x.CreatedAt, x.UploadedById }).ToListAsync(ct));
    }

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId, CancellationToken ct)
    {
        var ticket = await context.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return NotFound(new { message = "Ticket not found." });
        if (!await CanAccessTicketAsync(ticket, ct)) return Forbid();
        var attachment = await context.TicketAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && x.TicketId == id, ct);
        if (attachment is null || !System.IO.File.Exists(attachment.FilePath)) return NotFound(new { message = "Attachment not found." });
        var bytes = await System.IO.File.ReadAllBytesAsync(attachment.FilePath, ct);
        return File(bytes, attachment.FileType ?? "application/octet-stream", attachment.FileName);
    }

}

public sealed class UpdateStatusRequest { public string Status { get; set; } = ""; public string? Notes { get; set; } }
public sealed class AssignTicketRequest { public int TeamId { get; set; } public int AssignedToId { get; set; } public string? Notes { get; set; } }
public sealed class AddCommentRequest { public string Comment { get; set; } = ""; public bool IsInternal { get; set; } }

public sealed class CreateTicketRequest
{
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public string? CustomerRegion { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Source { get; set; } = "Phone";
}
