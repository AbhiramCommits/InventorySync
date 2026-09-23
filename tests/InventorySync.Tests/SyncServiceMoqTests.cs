using FluentAssertions;

using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Data;
using InventorySync.Infrastructure.Repositories;
using InventorySync.Infrastructure.Services;
using InventorySync.Tests.TestHelpers;

using Microsoft.Extensions.Options;

using Moq;

namespace InventorySync.Tests;

public class SyncServiceMoqTests
{
    private readonly Mock<IErpClient> _erpClient = new();

    [Fact]
    public async Task InsertPath_InsertsRowsAndAudits()
    {
        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-000001", quantityOnHand: "5"),
                ErpRecordFactory.Item("SKU-000002", quantityOnHand: "8"),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsInserted.Should().Be(2);
        run.RecordsUpdated.Should().Be(0);
        run.RecordsFailed.Should().Be(0);
        run.Status.Should().Be(SyncRunStatus.Succeeded);

        db.InventoryItems.Should().HaveCount(2);
        db.SyncAuditEntries.Should().OnlyContain(e => e.Action == SyncAuditAction.Insert);
        _erpClient.Verify(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePath_WritesPerFieldAuditEntries()
    {
        await using var db = InMemoryDb.Create();
        SeedExisting(db, ErpRecordFactory.Item("SKU-000001", quantityOnHand: "10", unitCost: "12.5000", modifiedDtm: "20240101120000"));

        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-000001", quantityOnHand: "25", unitCost: "12.5000", modifiedDtm: "20240102120000"),
            });

        var service = CreateService(db);

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsUpdated.Should().Be(1);
        run.RecordsInserted.Should().Be(0);

        db.InventoryItems.Single().QuantityOnHand.Should().Be(25);

        db.SyncAuditEntries.Should().ContainSingle(e =>
            e.Action == SyncAuditAction.Update
            && e.FieldName == "QuantityOnHand"
            && e.OldValue == "10"
            && e.NewValue == "25");
    }

    [Fact]
    public async Task SkipPath_IdenticalRecords_WriteSkipAuditAndTouchNothing()
    {
        await using var db = InMemoryDb.Create();
        SeedExisting(db, ErpRecordFactory.Item("SKU-000001", quantityOnHand: "10", modifiedDtm: "20240101120000"));

        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-000001", quantityOnHand: "10", modifiedDtm: "20240101120000"),
            });

        var service = CreateService(db);

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsUpdated.Should().Be(0);
        run.RecordsInserted.Should().Be(0);
        db.SyncAuditEntries.Should().ContainSingle(e => e.Action == SyncAuditAction.Skip);
        db.InventoryItems.Single().LastSyncedUtc.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task MalformedRecord_IsIsolated_AndRunMarksPartialSuccess()
    {
        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-000001"),
                ErpRecordFactory.Item("SKU-BAD", quantityOnHand: "NOT-A-NUMBER"),
                ErpRecordFactory.Item("SKU-000003"),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var run = await service.SyncInventoryAsync("unit-test");

        run.Status.Should().Be(SyncRunStatus.PartialSuccess);
        run.RecordsRead.Should().Be(3);
        run.RecordsInserted.Should().Be(2);
        run.RecordsFailed.Should().Be(1);

        db.InventoryItems.Select(i => i.Sku).Should().BeEquivalentTo(new[] { "SKU-000001", "SKU-000003" });
        db.SyncAuditEntries.Should().ContainSingle(e => e.Action == SyncAuditAction.Error && e.EntityKey == "SKU-BAD");
    }

    [Fact]
    public async Task ClientException_Propagates_AndMarksRunFailed()
    {
        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ERP unreachable"));

        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var act = () => service.SyncInventoryAsync("unit-test");

        await act.Should().ThrowAsync<HttpRequestException>();

        db.SyncRuns.Single().Status.Should().Be(SyncRunStatus.Failed);
        db.SyncRuns.Single().CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Batching_ProcessesRecordsAcrossMultipleChunks()
    {
        var records = Enumerable.Range(1, 25)
            .Select(i => ErpRecordFactory.Item($"SKU-{i:D6}"))
            .ToList();

        _erpClient
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        await using var db = InMemoryDb.Create();
        var service = CreateService(db, pageSize: 10);

        var run = await service.SyncInventoryAsync("unit-test");

        run.RecordsRead.Should().Be(25);
        run.RecordsInserted.Should().Be(25);
        db.InventoryItems.Should().HaveCount(25);
    }

    [Fact]
    public async Task PurchaseOrderSync_UsesClientAndInsertsLines()
    {
        _erpClient
            .Setup(c => c.GetPurchaseOrdersAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Order("PO-000001", status: "SUB", lines: new[]
                {
                    ErpRecordFactory.Line("SKU-A", "10", "2", "1.5000"),
                }),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db);

        var run = await service.SyncPurchaseOrdersAsync("unit-test");

        run.RecordsInserted.Should().Be(1);
        db.PurchaseOrders.Should().ContainSingle();
        db.PurchaseOrderLines.Should().ContainSingle();
        _erpClient.Verify(c => c.GetPurchaseOrdersAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private SyncService CreateService(SyncDbContext db, int pageSize = 500)
    {
        var options = Options.Create(new SyncOptions
        {
            PageSize = pageSize,
            ConflictResolution = ConflictResolutionStrategy.NewerWins,
        });

        return new SyncService(
            new InventoryItemRepository(db),
            new PurchaseOrderRepository(db),
            new SyncRepository(db),
            _erpClient.Object,
            options);
    }

    private static void SeedExisting(SyncDbContext db, InventorySync.Core.Dtos.Erp.ErpInventoryItemRecord record)
    {
        var mapper = InventorySync.Infrastructure.Erp.ErpRecordMapper.MapInventoryItem(record);
        db.InventoryItems.Add(mapper.Value!);
        db.SaveChanges();
    }
}
