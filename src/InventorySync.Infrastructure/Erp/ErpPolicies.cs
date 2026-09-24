using System.Net;

using Polly;
using Polly.Extensions.Http;

namespace InventorySync.Infrastructure.Erp;

/// <summary>
/// Polly resilience policies shared by the ERP HTTP client.
/// </summary>
public static class ErpPolicies
{
    /// <summary>
    /// Creates the retry policy: three attempts with exponential backoff and jitter.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> RetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                2,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)));
    }

    /// <summary>
    /// Creates the circuit breaker policy.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
    }
}
