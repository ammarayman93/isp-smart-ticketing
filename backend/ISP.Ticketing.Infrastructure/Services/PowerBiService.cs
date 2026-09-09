using System.Net.Http.Headers;
using System.Text.Json;
using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ISP.Ticketing.Infrastructure.Services;

public sealed class PowerBiService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PowerBiService> logger) : IPowerBiService
{
    private const string PowerBiResource = "https://analysis.windows.net/powerbi/api/.default";

    public async Task<PowerBiEmbedConfig> GetEmbedConfigAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = Required("PowerBI:TenantId");
        var clientId = Required("PowerBI:ClientId");
        var clientSecret = Required("PowerBI:ClientSecret");
        var workspaceId = Required("PowerBI:WorkspaceId");
        var reportId = Required("PowerBI:ReportId");

        var http = httpClientFactory.CreateClient("PowerBI");
        var aadToken = await GetMicrosoftEntraTokenAsync(http, tenantId, clientId, clientSecret, cancellationToken);

        using var reportRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{reportId}");
        reportRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aadToken);

        using var reportResponse = await http.SendAsync(reportRequest, cancellationToken);
        var reportBody = await reportResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!reportResponse.IsSuccessStatusCode)
        {
            logger.LogError("Power BI report metadata request failed. Status={StatusCode}, Body={Body}",
                reportResponse.StatusCode, reportBody);
            throw new InvalidOperationException("Power BI report metadata could not be loaded.");
        }

        using var reportJson = JsonDocument.Parse(reportBody);
        var reportRoot = reportJson.RootElement;
        var embedUrl = reportRoot.GetProperty("embedUrl").GetString();
        var datasetId = reportRoot.TryGetProperty("datasetId", out var datasetElement)
            ? datasetElement.GetString()
            : configuration["PowerBI:DatasetId"];

        if (string.IsNullOrWhiteSpace(embedUrl))
            throw new InvalidOperationException("Power BI report did not return an embed URL.");

        var tokenBody = JsonSerializer.Serialize(new { accessLevel = "View" });
        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{reportId}/GenerateToken")
        {
            Content = new StringContent(tokenBody, System.Text.Encoding.UTF8, "application/json")
        };
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aadToken);

        using var tokenResponse = await http.SendAsync(tokenRequest, cancellationToken);
        var tokenResponseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            logger.LogError("Power BI embed token request failed. Status={StatusCode}, Body={Body}",
                tokenResponse.StatusCode, tokenResponseBody);
            throw new InvalidOperationException("Power BI embed token could not be generated.");
        }

        using var tokenJson = JsonDocument.Parse(tokenResponseBody);
        var tokenRoot = tokenJson.RootElement;
        var embedToken = tokenRoot.GetProperty("token").GetString();
        var expiration = tokenRoot.TryGetProperty("expiration", out var expirationElement)
            ? expirationElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(embedToken))
            throw new InvalidOperationException("Power BI returned an empty embed token.");

        return new PowerBiEmbedConfig(
            reportId,
            embedUrl,
            embedToken,
            expiration,
            datasetId);
    }

    private static async Task<string> GetMicrosoftEntraTokenAsync(
        HttpClient http,
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = PowerBiResource,
            ["grant_type"] = "client_credentials"
        });

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Microsoft Entra authentication for Power BI failed.");

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Microsoft Entra returned an empty access token.");

        return token;
    }

    private string Required(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Power BI setting '{key}' is not configured.");
        return value;
    }
}
