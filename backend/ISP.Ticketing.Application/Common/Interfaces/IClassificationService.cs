using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Enums;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface IClassificationService
{
    Task<Result<ClassificationResult>> ClassifyAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default);
}

public class ClassificationResult
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public TicketPriority Priority { get; set; }
    public int SlaHours { get; set; }
    public decimal Confidence { get; set; }
    public string ModelVersion { get; set; } = "v1.0";
    public int ProcessingTimeMs { get; set; }
}