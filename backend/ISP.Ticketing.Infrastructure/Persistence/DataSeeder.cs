using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ISP.Ticketing.Infrastructure.Services;
using System.Text.Json;

namespace ISP.Ticketing.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        await EnsureRolesAsync(db);
        await EnsureCategoriesAsync(db);
        await EnsureTeamsAsync(db);
        await EnsureUsersAsync(db);
        await EnsureKnowledgeArticlesAsync(db);
    }

    private static async Task EnsureRolesAsync(ApplicationDbContext db)
    {
        var roles = new[]
        {
            ("Admin", "System administrator"),
            ("Supervisor", "Support supervisor"),
            ("Agent", "Support agent")
        };

        foreach (var role in roles)
        {
            if (!await db.Roles.AnyAsync(x => x.Name == role.Item1))
            {
                db.Roles.Add(new Role
                {
                    Name = role.Item1,
                    Description = role.Item2
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureCategoriesAsync(ApplicationDbContext db)
    {
        var categories = new[]
        {
            ("Service Outage", "انقطاع الخدمة", TicketPriority.Critical, 4),
            ("Slow Speed", "بطء السرعة", TicketPriority.High, 8),
            ("Router Issues", "مشاكل الراوتر", TicketPriority.Medium, 12),
            ("WiFi Problems", "مشاكل الواي فاي", TicketPriority.Medium, 12),
            ("Billing", "الفوترة", TicketPriority.Low, 24),
            ("Other", "أخرى", TicketPriority.Medium, 24)
        };

        foreach (var c in categories)
        {
            if (!await db.Categories.AnyAsync(x => x.Name == c.Item1))
            {
                db.Categories.Add(new Category
                {
                    Name = c.Item1,
                    NameAr = c.Item2,
                    DefaultPriority = c.Item3,
                    SlaHours = c.Item4
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureTeamsAsync(ApplicationDbContext db)
    {
        var teamNames = new[]
        {
            "Network Operations",
            "Technical Support",
            "Billing"
        };

        foreach (var name in teamNames)
        {
            if (!await db.Teams.AnyAsync(x => x.Name == name))
            {
                db.Teams.Add(new Team
                {
                    Name = name,
                    Description = $"{name} team",
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUsersAsync(ApplicationDbContext db)
    {
        var adminRole = await db.Roles.FirstAsync(x => x.Name == "Admin");
        var supervisorRole = await db.Roles.FirstAsync(x => x.Name == "Supervisor");
        var agentRole = await db.Roles.FirstAsync(x => x.Name == "Agent");

        var networkTeam = await db.Teams.FirstAsync(x => x.Name == "Network Operations");
        var supportTeam = await db.Teams.FirstAsync(x => x.Name == "Technical Support");
        var billingTeam = await db.Teams.FirstAsync(x => x.Name == "Billing");

        await EnsureUserAsync(db, new User
        {
            EmployeeCode = "EMP001",
            FullName = "System Administrator",
            Email = "admin@isp.com",
            PasswordHash = PasswordService.Hash("123456"),
            RoleId = adminRole.Id,
            IsActive = true,
            MaxConcurrentTickets = 50
        });

        await EnsureUserAsync(db, new User
        {
            EmployeeCode = "EMP002",
            FullName = "Support Agent",
            Email = "agent@isp.com",
            PasswordHash = PasswordService.Hash("123456"),
            RoleId = agentRole.Id,
            TeamId = supportTeam.Id,
            IsActive = true,
            MaxConcurrentTickets = 10
        });

        await EnsureUserAsync(db, new User
        {
            EmployeeCode = "EMP003",
            FullName = "Support Supervisor",
            Email = "supervisor@isp.com",
            PasswordHash = PasswordService.Hash("123456"),
            RoleId = supervisorRole.Id,
            TeamId = supportTeam.Id,
            IsActive = true,
            MaxConcurrentTickets = 30
        });

        await EnsureUserAsync(db, new User
        {
            EmployeeCode = "EMP004",
            FullName = "Network Agent",
            Email = "network@isp.com",
            PasswordHash = PasswordService.Hash("123456"),
            RoleId = agentRole.Id,
            TeamId = networkTeam.Id,
            IsActive = true,
            MaxConcurrentTickets = 10
        });

        await EnsureUserAsync(db, new User
        {
            EmployeeCode = "EMP005",
            FullName = "Billing Agent",
            Email = "billing@isp.com",
            PasswordHash = PasswordService.Hash("123456"),
            RoleId = agentRole.Id,
            TeamId = billingTeam.Id,
            IsActive = true,
            MaxConcurrentTickets = 10
        });
    }

    private static async Task EnsureKnowledgeArticlesAsync(ApplicationDbContext db)
    {
        if (await db.KnowledgeArticles.AnyAsync()) return;
        var articles = new[]
        {
            new KnowledgeArticle { Title = "Service outage - first checks", TitleAr = "انقطاع الخدمة - الفحوصات الأولية", Category = "Service Outage", Summary = "Verify whether the issue affects one customer or multiple customers.", Solution = "Check the access link, upstream device, interface state and recent alarms before restarting equipment.", StepsJson = JsonSerializer.Serialize(new[] { "Check the customer session and last online time.", "Check the access switch/radio/OLT interface and alarms.", "Compare with nearby customers in the same region.", "Link the ticket to the active outage when applicable." }) },
            new KnowledgeArticle { Title = "Slow internet speed", TitleAr = "بطء سرعة الإنترنت", Category = "Slow Speed", Summary = "Speed problems can be caused by congestion, wireless conditions or an incorrect profile.", Solution = "Verify the subscribed profile and actual throughput, then check link quality and congestion.", StepsJson = JsonSerializer.Serialize(new[] { "Run a speed test from a wired device.", "Compare actual speed with the subscribed package.", "Check signal/link quality and interface errors.", "Check peak-hour congestion." }) },
            new KnowledgeArticle { Title = "Router connectivity", TitleAr = "مشكلة اتصال الراوتر", Category = "Router Issues", Summary = "Router problems often come from power, WAN configuration, DHCP/PPPoE or firmware.", Solution = "Verify power and WAN status, then validate PPPoE/DHCP credentials and local network settings.", StepsJson = JsonSerializer.Serialize(new[] { "Verify power and link LEDs.", "Check WAN cable and port negotiation.", "Validate PPPoE/DHCP configuration.", "Test with a known-good cable or router." }) },
            new KnowledgeArticle { Title = "Wi-Fi coverage and instability", TitleAr = "ضعف أو عدم استقرار الواي فاي", Category = "WiFi Problems", Summary = "Wireless issues are commonly related to interference, channel selection or coverage.", Solution = "Move to a less congested channel, optimize radio placement and verify 2.4/5 GHz behavior.", StepsJson = JsonSerializer.Serialize(new[] { "Test near the router and at the affected location.", "Check channel utilization/interference.", "Test 2.4 GHz and 5 GHz separately.", "Adjust router placement." }) },
            new KnowledgeArticle { Title = "Billing issue", TitleAr = "مشكلة الفوترة", Category = "Billing", Summary = "Verify the customer's package, renewal date, payments and account status.", Solution = "Compare the account ledger with the active subscription and correct any mismatch through the billing workflow.", StepsJson = JsonSerializer.Serialize(new[] { "Verify customer account ID.", "Check payment and renewal history.", "Verify active package and expiry date.", "Escalate financial corrections to Billing." }) },
            new KnowledgeArticle { Title = "General technical issue", TitleAr = "مشكلة تقنية عامة", Category = "Other", Summary = "Use this article when the issue does not match a specialized category.", Solution = "Collect symptoms, affected equipment, timestamps and region, then route to the appropriate team.", StepsJson = JsonSerializer.Serialize(new[] { "Collect exact symptoms.", "Record affected device and connection type.", "Check recent changes or alarms.", "Escalate when the category becomes clear." }) }
        };
        db.KnowledgeArticles.AddRange(articles);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(ApplicationDbContext db, User seed)
    {
        var existing = await db.Users.FirstOrDefaultAsync(x => x.Email == seed.Email);
        if (existing is null)
        {
            db.Users.Add(seed);
            await db.SaveChangesAsync();
            return;
        }

        // Do not overwrite an existing user's password or personal changes.
        existing.EmployeeCode = seed.EmployeeCode;
        existing.FullName = seed.FullName;
        existing.RoleId = seed.RoleId;
        existing.TeamId = seed.TeamId;
        existing.IsActive = true;
        existing.MaxConcurrentTickets = seed.MaxConcurrentTickets;
        await db.SaveChangesAsync();
    }
}
