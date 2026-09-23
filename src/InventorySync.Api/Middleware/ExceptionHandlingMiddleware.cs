using InventorySync.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

            var (statusCode, title) = ex switch
            {
                EntityNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message),
                InvalidOperationException or ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
            };

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }
}
