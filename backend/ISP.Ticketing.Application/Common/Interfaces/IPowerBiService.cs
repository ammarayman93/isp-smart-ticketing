using ISP.Ticketing.Application.Common.Models;

namespace ISP.Ticketing.Application.Common.Interfaces;

public interface IPowerBiService
{
    Task<PowerBiEmbedConfig> GetEmbedConfigAsync(CancellationToken cancellationToken = default);
}
