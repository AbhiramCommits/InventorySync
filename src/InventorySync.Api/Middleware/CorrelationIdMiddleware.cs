using InventorySync.Core;

using Serilog.Context;

namespace InventorySync.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation id: it reuses the incoming
/// <c>X-Correlation-Id</c> header when present, otherwise generates one,
/// pushes it into the Serilog log scope, echoes it on the response, and
/// stores it in <see cref="HttpContext.Items"/> so downstream services (such
/// as the ERP client) can propagate it to outbound calls.
/// </summary>
public class CorrelationIdMiddleware
{
    private const int MaxHeaderLength = 64;

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes the middleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the request within a correlation-id log scope.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[CorrelationHeaders.ItemKey] = correlationId;
        context.Response.Headers[CorrelationHeaders.HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        var header = context.Request.Headers[CorrelationHeaders.HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(header)
            && header.Length <= MaxHeaderLength
            && header.All(c => !char.IsControl(c)))
        {
            return header.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }
}
