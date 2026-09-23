using InventorySync.Core.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace InventorySync.Api.Health;

public class ErpHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ErpOptions> _options;

    public ErpHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ErpOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = new Uri(_options.Value.BaseUrl, UriKind.Absolute);
            var client = _httpClientFactory.CreateClient("erp-health");
            client.Timeout = TimeSpan.FromSeconds(5);

            using var response = await client.GetAsync(new Uri(baseUrl, "/health"), ct);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("ERP system is reachable.")
                : HealthCheckResult.Degraded($"ERP system returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("ERP system is unreachable.", ex);
        }
    }
}
