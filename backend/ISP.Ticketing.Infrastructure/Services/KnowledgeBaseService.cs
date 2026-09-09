using System.Text.Json;
using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Application.Common.Models;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class KnowledgeBaseService(ApplicationDbContext db) : IKnowledgeBaseService
{
    public async Task<Result<IReadOnlyList<KnowledgeArticleResult>>> RecommendAsync(string title, string description, int? categoryId = null, CancellationToken cancellationToken = default)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        var articles = await db.KnowledgeArticles.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var ranked = articles.Select(a => new { Article = a, Score = Score(a, text) })
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Article.Id).Take(3)
            .Select(x => new KnowledgeArticleResult { Id = x.Article.Id, Title = x.Article.Title, TitleAr = x.Article.TitleAr, Category = x.Article.Category, Summary = x.Article.Summary, Solution = x.Article.Solution, Steps = ParseSteps(x.Article.StepsJson), Relevance = Math.Round(Math.Min(.99, x.Score), 2) }).ToList();
        return Result<IReadOnlyList<KnowledgeArticleResult>>.Success(ranked);
    }
    private static string[] ParseSteps(string json) { try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); } catch { return Array.Empty<string>(); } }
    private static double Score(KnowledgeArticle article, string text)
    {
        var keywords = article.Category switch
        {
            "Service Outage" => new[] { "outage", "انقطاع", "down", "متوقف", "لا يوجد انترنت", "internet", "مقطوع" },
            "Slow Speed" => new[] { "slow", "بطء", "سرعة", "speed", "throughput", "ضعيف", "بطيء" },
            "Router Issues" => new[] { "router", "راوتر", "wan", "pppoe", "dhcp", "مودم" },
            "WiFi Problems" => new[] { "wifi", "واي فاي", "wireless", "لاسلكي", "signal", "اشارة" },
            "Billing" => new[] { "billing", "فاتورة", "دفع", "payment", "renewal", "اشتراك", "تجديد" },
            _ => new[] { "other", "اخرى", "مشكلة", "استفسار" }
        };
        var hits = keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        return hits == 0 ? 0 : Math.Min(.98, .45 + hits * .12);
    }
}
