using ISP.Ticketing.Application.Common.Models;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface IKnowledgeBaseService
{
    Task<Result<IReadOnlyList<KnowledgeArticleResult>>> RecommendAsync(
        string title,
        string description,
        int? categoryId = null,
        CancellationToken cancellationToken = default);
}

public sealed class KnowledgeArticleResult
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string TitleAr { get; init; } = "";
    public string Category { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Solution { get; init; } = "";
    public string[] Steps { get; init; } = Array.Empty<string>();
    public double Relevance { get; init; }
}
