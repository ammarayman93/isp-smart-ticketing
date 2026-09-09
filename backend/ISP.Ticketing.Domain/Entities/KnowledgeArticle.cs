namespace ISP.Ticketing.Domain.Entities;

public class KnowledgeArticle
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string TitleAr { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string Solution { get; set; } = null!;
    public string StepsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public User? CreatedBy { get; set; }
}
