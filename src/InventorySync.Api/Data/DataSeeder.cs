using System.Diagnostics;
using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Erp;
using Microsoft.EntityFrameworkCore;

namespace InventorySync.Api.Data;

public static class DataSeeder
{
    public static async Task RunAsync(string[] args, IServiceProvider services)
    {
        var itemCount = 50_000;
        var poCount = 5_000;

        var seedIndex = Array.FindIndex(args, a => string.Equals(a, "seed", StringComparison.OrdinalIgnoreCase));
        if (seedIndex >= 0)
        {
            if (seedIndex + 1 < args.Length && int.TryParse(args[seedIndex + 1], out var parsedItems) && parsedItems > 0)
            {
                itemCount = parsedItems;
            }

            if (seedIndex + 2 < args.Length && int.TryParse(args[seedIndex + 2], out var parsedPos) && parsedPos > 0)
            {
                poCount = parsedPos;
            }
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (await db.InventoryItems.AnyAsync())
        {
            Console.WriteLine("The database already contains inventory items; skipping seed.");
            return;
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"Seeding {itemCount:N0} inventory items and {poCount:N0} purchase orders using seed {ErpDataGenerator.DefaultSeed}...");

        var inventoryRecords = ErpDataGenerator.GenerateInventory(itemCount, ErpDataGenerator.DefaultSeed).ToList();
        var seededItems = 0;
        foreach (var chunk in inventoryRecords.Chunk(1000))
        {
            db.InventoryItems.AddRange(chunk.Select(ToEntity));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            seededItems += chunk.Length;
            Console.WriteLine($"  Seeded {seededItems:N0}/{itemCount:N0} inventory items.");
        }

        var poRecords = ErpDataGenerator.GeneratePurchaseOrders(poCount, ErpDataGenerator.DefaultSeed, itemCount).ToList();
        var seededOrders = 0;
        foreach (var chunk in poRecords.Chunk(200))
        {
            db.PurchaseOrders.AddRange(chunk.Select(ToEntity));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            seededOrders += chunk.Length;
            Console.WriteLine($"  Seeded {seededOrders:N0}/{poCount:N0} purchase orders.");
        }

        stopwatch.Stop();
        Console.WriteLine($"Seed completed in {stopwatch.Elapsed}. Items: {seededItems:N0}, purchase orders: {seededOrders:N0}.");
    }

    private static InventoryItem ToEntity(ErpInventoryRecord record)
    {
        return new InventoryItem
        {
            Sku = record.Sku,
            Name = record.Name,
            Description = record.Description,
            QuantityOnHand = record.QuantityOnHand,
            UnitCost = record.UnitCost,
            WarehouseCode = record.WarehouseCode,
            ErpRecordId = record.ErpRecordId,
            LastSyncedUtc = record.ModifiedUtc,
        };
    }

    private static PurchaseOrder ToEntity(ErpPurchaseOrderRecord record)
    {
        return new PurchaseOrder
        {
            PoNumber = record.PoNumber,
            VendorCode = record.VendorCode,
            Status = record.Status,
            OrderDateUtc = record.OrderDateUtc,
            ExpectedDateUtc = record.ExpectedDateUtc,
            TotalAmount = record.TotalAmount,
            ErpRecordId = record.ErpRecordId,
            LastSyncedUtc = record.ModifiedUtc,
            Lines = record.Lines.Select(l => new PurchaseOrderLine
            {
                Sku = l.Sku,
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
    }
}
