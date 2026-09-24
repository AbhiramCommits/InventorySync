using System.Text.Json;
using System.Threading.RateLimiting;

using FluentValidation;
using FluentValidation.AspNetCore;

using InventorySync.Api.Data;
using InventorySync.Api.Health;
using InventorySync.Api.Middleware;
using InventorySync.Core.Interfaces;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Erp;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InventorySync API",
        Version = "v1",
        Description = "ERP integration service for inventory and purchase order synchronisation.",
    });

    foreach (var xmlFile in new[]
    {
        Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"),
        Path.Combine(AppContext.BaseDirectory, "InventorySync.Core.xml"),
        Path.Combine(AppContext.BaseDirectory, "InventorySync.Infrastructure.xml"),
    })
    {
        if (File.Exists(xmlFile))
        {
            options.IncludeXmlComments(xmlFile);
        }
    }
});

builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Set it via the "
                + "ConnectionStrings__DefaultConnection environment variable or user-secrets."),
        sql => sql.MigrationsAssembly(typeof(SyncDbContext).Assembly.FullName)));

builder.Services.Configure<ErpOptions>(builder.Configuration.GetSection(ErpOptions.SectionName));
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));

var corsOrigins = builder.Configuration.GetSection(CorsOptions.SectionName)
    .GetSection(nameof(CorsOptions.AllowedOrigins))
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOptions.PolicyName, policy =>
    {
        if (corsOrigins.Length == 0 || corsOrigins.Contains("*", StringComparer.Ordinal))
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

builder.Services.AddHttpClient<IErpClient, ErpHttpClient>((sp, client) =>
    {
        var erpOptions = sp.GetRequiredService<IOptions<ErpOptions>>().Value;
        client.BaseAddress = new Uri(erpOptions.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(ErpPolicies.RetryPolicy())
    .AddPolicyHandler(ErpPolicies.CircuitBreakerPolicy());

builder.Services.AddHttpClient("erp-health");

builder.Services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<ISyncRepository, SyncRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISyncService, SyncService>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SyncDbContext>("database", tags: new[] { "db" })
    .AddCheck<ErpHealthCheck>("erp", tags: new[] { "erp" });

var rateLimitPermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 10);
var rateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = new { title = "Too many sync requests.", status = 429, detail = "Rate limit exceeded. Retry after a short wait." };
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), context.HttpContext.RequestAborted);
    };

    options.AddFixedWindowLimiter("sync", limiter =>
    {
        limiter.PermitLimit = rateLimitPermitLimit;
        limiter.Window = TimeSpan.FromSeconds(rateLimitWindowSeconds);
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.AddOutputCache();

var app = builder.Build();

if (args.Any(a => string.Equals(a, "seed", StringComparison.OrdinalIgnoreCase)))
{
    await DataSeeder.RunAsync(args, app.Services);
    return;
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseResponseCompression();
app.UseOutputCache();
app.UseRateLimiter();

app.UseCors(CorsOptions.PolicyName);
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync,
});

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString(),
            }),
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

public partial class Program
{
}
