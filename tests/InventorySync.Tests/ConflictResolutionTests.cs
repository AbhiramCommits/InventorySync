using FluentAssertions;

using InventorySync.Core.Enums;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

using Microsoft.Extensions.Options;

namespace InventorySync.Tests;

public class ConflictResolutionTests
{
    private static readonly DateTime ErpModified = new(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime LocalModifiedEarlier = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime LocalModifiedLater = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ErpWins_OverwritesLocalChange_EvenWhenLocalIsNewer()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.ErpWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = LocalModifiedLater;
        await db.SaveChangesAsync();

        var run = await service.SyncInventoryAsync("unit-test");

        db.InventoryItems.Single().QuantityOnHand.Should().Be(25);
        db.InventoryItems.Single().LocallyModifiedUtc.Should().BeNull();
        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "QuantityOnHand"
            && e.OldValue == "7"
            && e.NewValue == "25");
        run.RecordsUpdated.Should().Be(1);
    }

    [Fact]
    public async Task LocalWins_KeepsLocalChange_EvenWhenLocalIsOlder()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.LocalWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = LocalModifiedEarlier;
        await db.SaveChangesAsync();

        var run = await service.SyncInventoryAsync("unit-test");

        db.InventoryItems.Single().QuantityOnHand.Should().Be(7);
        db.InventoryItems.Single().LocallyModifiedUtc.Should().NotBeNull();
        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "QuantityOnHand"
            && e.OldValue == "25"
            && e.NewValue == "7");
        run.RecordsUpdated.Should().Be(1);
    }

    [Fact]
    public async Task NewerWins_ErpNewer_AppliesErpValue()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.NewerWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = LocalModifiedEarlier;
        await db.SaveChangesAsync();

        await service.SyncInventoryAsync("unit-test");

        db.InventoryItems.Single().QuantityOnHand.Should().Be(25);
        db.InventoryItems.Single().LocallyModifiedUtc.Should().BeNull();
        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "QuantityOnHand");
    }

    [Fact]
    public async Task NewerWins_LocalNewer_KeepsLocalValue()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.NewerWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = LocalModifiedLater;
        await db.SaveChangesAsync();

        await service.SyncInventoryAsync("unit-test");

        db.InventoryItems.Single().QuantityOnHand.Should().Be(7);
        db.InventoryItems.Single().LocallyModifiedUtc.Should().Be(LocalModifiedLater);
        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.ConflictResolved
            && e.FieldName == "QuantityOnHand"
            && e.OldValue == "25"
            && e.NewValue == "7");
    }

    [Fact]
    public async Task BothSidesChanged_ResolvesEachFieldIndependently()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item(
            "SKU-000001",
            quantityOnHand: "25",
            unitCost: "99.0000",
            modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.ErpWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.UnitCost = 1.25m;
        item.LocallyModifiedUtc = LocalModifiedLater;
        await db.SaveChangesAsync();

        await service.SyncInventoryAsync("unit-test");

        var updated = db.InventoryItems.Single();
        updated.QuantityOnHand.Should().Be(25);
        updated.UnitCost.Should().Be(99m);
        db.SyncAuditEntries.Where(e => e.Action == SyncAuditAction.ConflictResolved)
            .Select(e => e.FieldName)
            .Should().Contain(new[] { "QuantityOnHand", "UnitCost" });
    }

    [Fact]
    public async Task BothSidesChangedToSameValue_IsNotAConflict()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "7", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.ErpWins, fake);

        await service.SyncInventoryAsync("unit-test");

        var item = db.InventoryItems.Single();
        item.QuantityOnHand = 7;
        item.LocallyModifiedUtc = LocalModifiedLater;
        await db.SaveChangesAsync();

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsUpdated.Should().Be(0);
        db.SyncAuditEntries.Should().ContainSingle(e => e.Action == SyncAuditAction.Skip);
        db.SyncAuditEntries.Where(e => e.Action == SyncAuditAction.ConflictResolved).Should().BeEmpty();
    }

    [Fact]
    public async Task UnmodifiedLocalRow_ErpChangesApply_WithoutConflictEntries()
    {
        await using var db = InMemoryDb.Create();
        var fake = new FakeErpClient(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", modifiedDtm: "20240301120000"));
        var service = CreateService(db, ConflictResolutionStrategy.ErpWins, fake);

        await service.SyncInventoryAsync("unit-test");

        fake.ReplaceItem("SKU-000001", ErpRecordFactory.Item("SKU-000001", quantityOnHand: "30", modifiedDtm: "20240401120000"));

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsUpdated.Should().Be(1);
        db.InventoryItems.Single().QuantityOnHand.Should().Be(30);
        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.Update
            && e.FieldName == "QuantityOnHand"
            && e.OldValue == "25"
            && e.NewValue == "30");
        db.SyncAuditEntries.Where(e => e.Action == SyncAuditAction.ConflictResolved).Should().BeEmpty();
    }

    private static SyncService CreateService(SyncDbContext db, ConflictResolutionStrategy strategy, FakeErpClient fake)
    {
        var options = Options.Create(new SyncOptions { PageSize = 10, ConflictResolution = strategy });
        return new SyncService(
            new InventoryItemRepository(db),
            new PurchaseOrderRepository(db),
            new SyncRepository(db),
            fake,
            options);
    }
}
