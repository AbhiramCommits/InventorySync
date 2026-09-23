using System.Text.Json;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

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
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."),
        sql => sql.MigrationsAssembly(typeof(SyncDbContext).Assembly.FullName)));

builder.Services.Configure<ErpOptions>(builder.Configuration.GetSection(ErpOptions.SectionName));
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));

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

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

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
