namespace ISP.Ticketing.Domain.Entities;

public class ClassificationLog
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public int? PredictedCategoryId { get; set; }
    public string? PredictedPriority { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? ModelVersion { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public bool WasOverridden { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public Category? PredictedCategory { get; set; }
}