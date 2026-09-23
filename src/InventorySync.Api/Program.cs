using InventorySync.Api.Data;
using InventorySync.Api.Middleware;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Erp;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."),
        sql => sql.MigrationsAssembly(typeof(SyncDbContext).Assembly.FullName)));

builder.Services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<ISyncRepository, SyncRepository>();
builder.Services.AddSingleton<IErpConnector, StubErpConnector>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISyncService, SyncService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
