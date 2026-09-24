namespace InventorySync.Core;

/// <summary>
/// Shared identifiers used to propagate a correlation id through the API,
/// the service layer and outbound ERP calls.
/// </summary>
public static class CorrelationHeaders
{
    /// <summary>
    /// The HTTP header that carries the correlation id.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// The <c>HttpContext.Items</c> key under which the correlation id is stored
    /// for the current request.
    /// </summary>
    public const string ItemKey = "CorrelationId";
}
