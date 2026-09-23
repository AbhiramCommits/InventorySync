using InventorySync.Core.Enums;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Erp;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

namespace InventorySync.Tests;

public class SyncServiceTests
{
    [Fact]
    public async Task SyncInventory_FirstRun_InsertsAllRecords()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        var result = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(SyncRunStatus.Succeeded, result.Status);
        Assert.Equal(StubErpConnector.InventoryRecordCount, result.RecordsRead);
        Assert.Equal(StubErpConnector.InventoryRecordCount, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Equal(0, result.RecordsFailed);

        Assert.Equal(StubErpConnector.InventoryRecordCount, db.InventoryItems.Count());
        Assert.Equal(1, db.SyncRuns.Count());
        Assert.Equal(StubErpConnector.InventoryRecordCount, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.Insert));
        Assert.All(db.InventoryItems, i => Assert.NotNull(i.LastSyncedUtc));
    }

    [Fact]
    public async Task SyncInventory_SecondRun_SkipsEverything()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        await service.SyncInventoryAsync("unit-test");
        var result = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(SyncRunStatus.Succeeded, result.Status);
        Assert.Equal(StubErpConnector.InventoryRecordCount, result.RecordsRead);
        Assert.Equal(0, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Equal(StubErpConnector.InventoryRecordCount, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.Skip));
    }

    [Fact]
    public async Task SyncInventory_LocalChanges_AreOverwrittenAndAudited()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.First();
        var originalQuantity = item.QuantityOnHand;
        item.QuantityOnHand = originalQuantity + 42;
        await db.SaveChangesAsync();

        var result = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(originalQuantity, db.InventoryItems.First().QuantityOnHand);
        Assert.Equal(1, result.RecordsUpdated);
        Assert.Contains(db.SyncAuditEntries, e =>
            e.Action == SyncAuditAction.Update
            && e.FieldName == "QuantityOnHand"
            && e.EntityKey == item.Sku);
    }

    [Fact]
    public async Task SyncPurchaseOrders_InsertsOrdersAndLines()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        var result = await service.SyncPurchaseOrdersAsync("unit-test");

        Assert.Equal(SyncRunStatus.Succeeded, result.Status);
        Assert.Equal(StubErpConnector.PurchaseOrderRecordCount, result.RecordsRead);
        Assert.Equal(StubErpConnector.PurchaseOrderRecordCount, result.RecordsInserted);
        Assert.Equal(StubErpConnector.PurchaseOrderRecordCount, db.PurchaseOrders.Count());
        Assert.True(db.PurchaseOrderLines.Any());
        Assert.All(db.PurchaseOrders, po =>
        {
            Assert.InRange(po.Lines.Count, 1, 10);
            Assert.NotNull(po.LastSyncedUtc);
        });
    }

    [Fact]
    public async Task SyncPurchaseOrders_SecondRun_SkipsEverything()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        await service.SyncPurchaseOrdersAsync("unit-test");
        var lineCountAfterFirstRun = db.PurchaseOrderLines.Count();
        var result = await service.SyncPurchaseOrdersAsync("unit-test");

        Assert.Equal(0, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Equal(StubErpConnector.PurchaseOrderRecordCount, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.Skip && e.EntityType == SyncEntityType.PurchaseOrder));
        Assert.Equal(lineCountAfterFirstRun, db.PurchaseOrderLines.Count());
    }

    [Fact]
    public async Task SyncPurchaseOrders_PreservesLocallyReceivedQuantities()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        await service.SyncPurchaseOrdersAsync("unit-test");

        var order = db.PurchaseOrders.First(po => po.Lines.Any(l => l.QuantityOrdered > l.QuantityReceived));
        var line = order.Lines.First(l => l.QuantityOrdered > l.QuantityReceived);
        var localReceived = line.QuantityOrdered;
        line.QuantityReceived = localReceived;
        order.Status = PurchaseOrderStatus.Received;
        await db.SaveChangesAsync();

        await service.SyncPurchaseOrdersAsync("unit-test");

        Assert.Equal(PurchaseOrderStatus.Received, db.PurchaseOrders.First(po => po.Id == order.Id).Status);
        Assert.Equal(localReceived, db.PurchaseOrderLines.First(l => l.Id == line.Id).QuantityReceived);
        Assert.Contains(db.SyncAuditEntries, e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.EntityKey == order.PoNumber
            && e.FieldName == $"Lines/{line.Sku}/QuantityReceived");
    }

    [Fact]
    public async Task SyncRuns_AreDeterministicAcrossContexts()
    {
        await using var db1 = InMemoryDb.Create();
        await using var db2 = InMemoryDb.Create();

        await CreateSyncService(db1).SyncInventoryAsync("unit-test");
        await CreateSyncService(db2).SyncInventoryAsync("unit-test");

        var skus1 = db1.InventoryItems.Select(i => i.Sku).OrderBy(s => s).ToList();
        var skus2 = db2.InventoryItems.Select(i => i.Sku).OrderBy(s => s).ToList();
        Assert.Equal(skus1, skus2);
    }

    [Fact]
    public async Task GetRunsAndAuditEntries_ReturnSyncedData()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db);

        var result = await service.SyncInventoryAsync("unit-test");

        var runs = await service.GetRunsAsync(SyncEntityType.InventoryItem, 1, 10);
        Assert.Single(runs.Items);
        Assert.Equal(result.SyncRunId, runs.Items[0].Id);

        var audits = await service.GetAuditEntriesAsync(result.SyncRunId, 1, 10);
        Assert.Equal(10, audits.Count);
        Assert.All(audits, a => Assert.Equal(result.SyncRunId, a.SyncRunId));
    }

    private static SyncService CreateSyncService(SyncDbContext db)
    {
        return new SyncService(
            new InventoryItemRepository(db),
            new PurchaseOrderRepository(db),
            new SyncRepository(db),
            new StubErpConnector());
    }
}
