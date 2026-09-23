using InventorySync.Api.Data;
using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySync.Tests;

public class DataSeederTests
{
    [Fact]
    public async Task Seed_PopulatesRequestedCounts()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        await DataSeeder.RunAsync(new[] { "seed", "100", "20" }, services);

        Assert.Equal(100, db.InventoryItems.Count());
        Assert.Equal(20, db.PurchaseOrders.Count());
        Assert.Equal(20, db.PurchaseOrders.Count(po => po.Lines.Any()));
    }

    [Fact]
    public async Task Seed_IsDeterministic()
    {
        await using var services1 = CreateServices();
        await using var services2 = CreateServices();

        await DataSeeder.RunAsync(new[] { "seed", "250", "40" }, services1);
        await DataSeeder.RunAsync(new[] { "seed", "250", "40" }, services2);

        await using var scope1 = services1.CreateAsyncScope();
        await using var scope2 = services2.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<SyncDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<SyncDbContext>();

        var items1 = db1.InventoryItems.AsNoTracking().OrderBy(i => i.Sku).Select(i => new { i.Sku, i.Name, i.QuantityOnHand, i.UnitCost, i.WarehouseCode }).ToList();
        var items2 = db2.InventoryItems.AsNoTracking().OrderBy(i => i.Sku).Select(i => new { i.Sku, i.Name, i.QuantityOnHand, i.UnitCost, i.WarehouseCode }).ToList();
        Assert.Equal(items1, items2);

        var pos1 = db1.PurchaseOrders.AsNoTracking().OrderBy(p => p.PoNumber).Select(p => p.PoNumber).ToList();
        var pos2 = db2.PurchaseOrders.AsNoTracking().OrderBy(p => p.PoNumber).Select(p => p.PoNumber).ToList();
        Assert.Equal(pos1, pos2);
    }

    [Fact]
    public async Task Seed_SkipsWhenDataExists()
    {
        await using var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        await DataSeeder.RunAsync(new[] { "seed", "10", "5" }, services);
        var countBefore = db.InventoryItems.Count();

        await DataSeeder.RunAsync(new[] { "seed", "500", "100" }, services);

        Assert.Equal(countBefore, db.InventoryItems.Count());
    }

    private static ServiceProvider CreateServices()
    {
        var databaseName = $"seeder-tests-{Guid.NewGuid():N}";
        var collection = new ServiceCollection();
        collection.AddDbContext<SyncDbContext>(o => o.UseInMemoryDatabase(databaseName));
        return collection.BuildServiceProvider();
    }
}
