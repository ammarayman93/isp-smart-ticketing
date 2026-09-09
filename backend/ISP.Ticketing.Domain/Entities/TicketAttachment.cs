namespace ISP.Ticketing.Domain.Entities;

public class TicketAttachment
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string? FileType { get; set; }
    public int? FileSize { get; set; }
    public int UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}