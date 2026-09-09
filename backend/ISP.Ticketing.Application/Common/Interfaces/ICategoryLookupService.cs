using ISP.Ticketing.Domain.Entities;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface ICategoryLookupService
{
    Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
}
