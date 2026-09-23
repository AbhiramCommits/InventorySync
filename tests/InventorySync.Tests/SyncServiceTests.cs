using InventorySync.Core.Dtos;
using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Exceptions;
using InventorySync.Core.Interfaces;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InventorySync.Tests;

public class SyncServiceTests
{
    [Fact]
    public async Task SyncInventory_FirstRun_InsertsAllRecords()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001"), ErpRecordFactory.Item("SKU-000002") }));

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(SyncRunStatus.Succeeded, run.Status);
        Assert.Equal(2, run.RecordsRead);
        Assert.Equal(2, run.RecordsInserted);
        Assert.Equal(0, run.RecordsUpdated);
        Assert.Equal(0, run.RecordsFailed);
        Assert.Equal(2, db.InventoryItems.Count());
        Assert.Equal(2, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.Insert));
        Assert.All(db.InventoryItems, i => Assert.NotNull(i.LastSyncedUtc));
    }

    [Fact]
    public async Task SyncInventory_SecondRun_SkipsEverything()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001") }));

        await service.SyncInventoryAsync("unit-test");
        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(0, run.RecordsInserted);
        Assert.Equal(0, run.RecordsUpdated);
        Assert.Equal(1, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.Skip));
    }

    [Fact]
    public async Task SyncInventory_ChangedFields_WriteFieldLevelAudits()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(items: ErpRecordFactory.Item("SKU-000001", quantityOnHand: "10", unitCost: "12.5000"));
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        await service.SyncInventoryAsync("unit-test");

        fake.ReplaceItem("SKU-000001", ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", unitCost: "12.5000", modifiedDtm: "20240102120000"));

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(1, run.RecordsUpdated);
        var audit = db.SyncAuditEntries.Single(e => e.Action == SyncAuditAction.Update);
        Assert.Equal("QuantityOnHand", audit.FieldName);
        Assert.Equal("10", audit.OldValue);
        Assert.Equal("25", audit.NewValue);
        Assert.Equal(25, db.InventoryItems.Single().QuantityOnHand);
    }

    [Theory]
    [InlineData(ConflictResolutionStrategy.ErpWins, 25)]
    [InlineData(ConflictResolutionStrategy.LocalWins, 7)]
    public async Task SyncInventory_Conflict_AppliesConfiguredStrategy(ConflictResolutionStrategy strategy, int expectedQuantity)
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, strategy, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25") }));

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(expectedQuantity, db.InventoryItems.Single().QuantityOnHand);
        Assert.Equal(1, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.ConflictResolved && e.FieldName == "QuantityOnHand"));
        Assert.Equal(strategy == ConflictResolutionStrategy.ErpWins, db.InventoryItems.Single().LocallyModifiedUtc is null);
    }

    [Fact]
    public async Task SyncInventory_NewerWins_LocalNewerKeepsLocalValue()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240110120000") }));

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(7, db.InventoryItems.Single().QuantityOnHand);
        Assert.Equal(1, db.SyncAuditEntries.Count(e => e.Action == SyncAuditAction.ConflictResolved));
        Assert.Equal(1, run.RecordsUpdated);
    }

    [Fact]
    public async Task SyncInventory_MalformedRecords_MarkRunPartialSuccess()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(
            db,
            ConflictResolutionStrategy.NewerWins,
            new FakeErpClient(items: new[]
            {
                ErpRecordFactory.Item("SKU-000001"),
                ErpRecordFactory.Item("SKU-BAD", quantityOnHand: "NOT-A-NUMBER"),
            }));

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(SyncRunStatus.PartialSuccess, run.Status);
        Assert.Equal(2, run.RecordsRead);
        Assert.Equal(1, run.RecordsInserted);
        Assert.Equal(1, run.RecordsFailed);
        Assert.Equal(1, db.InventoryItems.Count());

        var error = db.SyncAuditEntries.Single(e => e.Action == SyncAuditAction.Error);
        Assert.Equal("SKU-BAD", error.EntityKey);
        Assert.Contains("ITM_QTY_ON_HAND", error.Message);
    }

    [Fact]
    public async Task SyncInventory_PerRecordException_DoesNotAbortRun()
    {
        await using var db = InMemoryDb.Create();
        var inner = new InventoryItemRepository(db);
        var throwing = new ThrowingInventoryItemRepository(inner, "SKU-BOOM");
        var service = CreateSyncService(
            db,
            ConflictResolutionStrategy.NewerWins,
            new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001"), ErpRecordFactory.Item("SKU-BOOM") }),
            inventoryRepository: throwing);

        var run = await service.SyncInventoryAsync("unit-test");

        Assert.Equal(SyncRunStatus.PartialSuccess, run.Status);
        Assert.Equal(1, run.RecordsInserted);
        Assert.Equal(1, run.RecordsFailed);
        Assert.Single(db.InventoryItems);
        Assert.Single(db.SyncAuditEntries, e => e.Action == SyncAuditAction.Error && e.EntityKey == "SKU-BOOM");
    }

    [Fact]
    public async Task RetryFailedRecords_RetriesOnlyErroredKeys_AsChildRun()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001"), ErpRecordFactory.Item("SKU-BAD", quantityOnHand: "NOPE"));
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        var parent = await service.SyncInventoryAsync("unit-test");
        Assert.Equal(SyncRunStatus.PartialSuccess, parent.Status);

        fake.ReplaceItem("SKU-BAD", ErpRecordFactory.Item("SKU-BAD", quantityOnHand: "42"));

        var child = await service.RetryFailedRecordsAsync(parent.Id);

        Assert.Equal(parent.Id, child.ParentSyncRunId);
        Assert.Equal(SyncRunStatus.Succeeded, child.Status);
        Assert.Equal(1, child.RecordsRead);
        Assert.Equal(1, child.RecordsInserted);
        Assert.Equal(0, child.RecordsFailed);
        Assert.Contains("SKU-BAD", fake.LastRequestedSkus ?? Array.Empty<string>());
        Assert.Equal(2, db.InventoryItems.Count());
    }

    [Fact]
    public async Task RetryFailedRecords_NoErrors_CompletesImmediately()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001") }));

        var parent = await service.SyncInventoryAsync("unit-test");
        var child = await service.RetryFailedRecordsAsync(parent.Id);

        Assert.Equal(parent.Id, child.ParentSyncRunId);
        Assert.Equal(SyncRunStatus.Succeeded, child.Status);
        Assert.Equal(0, child.RecordsRead);
    }

    [Fact]
    public async Task RetryFailedRecords_ChildRun_Throws()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(items: ErpRecordFactory.Item("SKU-BAD", quantityOnHand: "NOPE"));
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        var parent = await service.SyncInventoryAsync("unit-test");
        var child = await service.RetryFailedRecordsAsync(parent.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetryFailedRecordsAsync(child.Id));
    }

    [Fact]
    public async Task SyncPurchaseOrders_InsertsOrdersAndLines_ThenSkips()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(new[] { ErpRecordFactory.Order("PO-000001", status: "SUB", lines: new[]
        {
            ErpRecordFactory.Line("SKU-A", "10", "0", "1.5000"),
            ErpRecordFactory.Line("SKU-B", "4", "0", "2.2500"),
        }),
});
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        var run = await service.SyncPurchaseOrdersAsync("unit-test");

        Assert.Equal(SyncRunStatus.Succeeded, run.Status);
        Assert.Equal(1, run.RecordsInserted);
        Assert.Equal(2, db.PurchaseOrderLines.Count());
        Assert.Equal(24m, db.PurchaseOrders.Single().TotalAmount);
        Assert.Equal(PurchaseOrderStatus.Submitted, db.PurchaseOrders.Single().Status);

        var second = await service.SyncPurchaseOrdersAsync("unit-test");
        Assert.Equal(0, second.RecordsInserted);
        Assert.Equal(0, second.RecordsUpdated);
    }

    [Fact]
    public async Task SyncPurchaseOrders_PreservesLocalReceiptsAndStatus()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(new[] { ErpRecordFactory.Order("PO-000001", status: "PAR", modifiedDtm: "20240101120000", lines: new[]
        {
            ErpRecordFactory.Line("SKU-A", "10", "3", "1.5000"),
        }),
});
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        await service.SyncPurchaseOrdersAsync("unit-test");

        var order = db.PurchaseOrders.Include(o => o.Lines).Single();
        order.Lines.Single().QuantityReceived = 10;
        order.Status = PurchaseOrderStatus.Received;
        order.LocallyModifiedUtc = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        await service.SyncPurchaseOrdersAsync("unit-test");

        order = db.PurchaseOrders.Include(o => o.Lines).Single();
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(10, order.Lines.Single().QuantityReceived);
        Assert.Contains(db.SyncAuditEntries, e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "Lines/SKU-A/QuantityReceived");
        Assert.Contains(db.SyncAuditEntries, e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "Status");
    }

    [Fact]
    public async Task GetRuns_GetRunById_And_AuditEntries_FilterAndPage()
    {
        await using var db = InMemoryDb.Create();
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, new FakeErpClient(items: new[] { ErpRecordFactory.Item("SKU-000001"), ErpRecordFactory.Item("SKU-000002") }));

        var run = await service.SyncInventoryAsync("unit-test");

        var runs = await service.GetRunsAsync(SyncEntityType.InventoryItem, SyncRunStatus.Succeeded, 1, 10);
        Assert.Single(runs.Items);

        var byId = await service.GetRunByIdAsync(run.Id);
        Assert.Equal(run.Status, byId.Status);

        var audits = await service.GetAuditEntriesAsync(run.Id, SyncAuditAction.Insert, 1, 1);
        Assert.Equal(2, audits.TotalCount);
        Assert.Single(audits.Items);
        Assert.All(audits.Items, a => Assert.Equal(SyncAuditAction.Insert, a.Action));
    }

    [Fact]
    public async Task SyncInventory_RecordsFieldLevelOldAndNewValues()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(items: ErpRecordFactory.Item("SKU-000001", unitCost: "10.0000"));
        var service = CreateSyncService(db, ConflictResolutionStrategy.NewerWins, fake);

        await service.SyncInventoryAsync("unit-test");

        fake.ReplaceItem("SKU-000001", ErpRecordFactory.Item("SKU-000001", unitCost: "99.9900", modifiedDtm: "20240201120000"));

        await service.SyncInventoryAsync("unit-test");

        var audit = db.SyncAuditEntries.Single(e => e.Action == SyncAuditAction.Update && e.FieldName == "UnitCost");
        Assert.Equal("10.0000", audit.OldValue);
        Assert.Equal("99.9900", audit.NewValue);
    }

    private static SyncService CreateSyncService(
        SyncDbContext db,
        ConflictResolutionStrategy strategy,
        FakeErpClient fake,
        IInventoryItemRepository? inventoryRepository = null)
    {
        var options = Options.Create(new SyncOptions
        {
            PageSize = 10,
            ConflictResolution = strategy,
        });

        return new SyncService(
            inventoryRepository ?? new InventoryItemRepository(db),
            new PurchaseOrderRepository(db),
            new SyncRepository(db),
            fake,
            options);
    }

    private sealed class ThrowingInventoryItemRepository : IInventoryItemRepository
    {
        private readonly IInventoryItemRepository _inner;
        private readonly string _explodingSku;

        public ThrowingInventoryItemRepository(IInventoryItemRepository inner, string explodingSku)
        {
            _inner = inner;
            _explodingSku = explodingSku;
        }

        public Task AddAsync(InventoryItem item, CancellationToken ct = default)
        {
            if (item.Sku == _explodingSku)
            {
                throw new InvalidOperationException($"Injected failure for {_explodingSku}.");
            }

            return _inner.AddAsync(item, ct);
        }

        public Task<PagedResult<InventoryItem>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default) => _inner.GetPagedAsync(query, ct);
        public Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);
        public Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default) => _inner.GetBySkuAsync(sku, ct);
        public Task<Dictionary<string, InventoryItem>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default) => _inner.GetBySkusAsync(skus, ct);
        public Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default) => _inner.SkuExistsAsync(sku, ct);
        public void Update(InventoryItem item) => _inner.Update(item);
        public void SetOriginalRowVersion(InventoryItem item, byte[] rowVersion) => _inner.SetOriginalRowVersion(item, rowVersion);
        public void Delete(InventoryItem item) => _inner.Delete(item);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    }
}
