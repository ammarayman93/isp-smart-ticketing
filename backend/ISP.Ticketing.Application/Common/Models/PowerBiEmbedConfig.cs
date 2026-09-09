namespace ISP.Ticketing.Application.Common.Models;

public sealed record PowerBiEmbedConfig(
    string ReportId,
    string EmbedUrl,
    string AccessToken,
    string? TokenExpiration,
    string? DatasetId);
