using ISP.Ticketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<OutageEvent> OutageEvents => Set<OutageEvent>();
    public DbSet<ClassificationLog> ClassificationLogs => Set<ClassificationLog>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ÚáÇÞÉ Team ãÚ Manager
        modelBuilder.Entity<Team>()
            .HasOne(t => t.Manager)
            .WithMany()
            .HasForeignKey(t => t.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // ÚáÇÞÉ User ãÚ Team
        modelBuilder.Entity<User>()
            .HasOne(u => u.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        // ÚáÇÞÉ User ãÚ Role
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // ÚáÇÞÉ Ticket ãÚ CreatedBy
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // ÚáÇÞÉ Ticket ãÚ AssignedTo
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        // ÈÇÞí ÇáÅÚÏÇÏÇÊ
        modelBuilder.Entity<KnowledgeArticle>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<KnowledgeArticle>()
            .Property(x => x.Title).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<KnowledgeArticle>()
            .Property(x => x.TitleAr).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<KnowledgeArticle>()
            .Property(x => x.Category).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<KnowledgeArticle>()
            .Property(x => x.StepsJson).HasColumnType("longtext");
        modelBuilder.Entity<KnowledgeArticle>()
            .HasOne(x => x.CreatedBy).WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}