using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventorySync.Api.Attributes;

/// <summary>
/// Computes a content-based ETag for a successful object result so the output
/// cache middleware can answer conditional requests with 304 Not Modified.
/// </summary>
public sealed class GenerateETagAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Wraps the action and attaches an ETag header to successful object results.
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Result is ObjectResult { Value: not null } objectResult
            && objectResult.StatusCode is null or >= 200 and < 300)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(objectResult.Value);
            var etag = $"\"{Convert.ToHexString(SHA1.HashData(payload))}\"";

            context.HttpContext.Response.Headers.ETag = etag;
        }
    }
}
