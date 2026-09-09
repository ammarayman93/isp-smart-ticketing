using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Domain.Entities;
using ISP.Ticketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class CategoryLookupService(ApplicationDbContext db) : ICategoryLookupService
{
    public async Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync(cancellationToken);
}
