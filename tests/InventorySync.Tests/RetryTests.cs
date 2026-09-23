using FluentAssertions;

using InventorySync.Core.Dtos.Erp;
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

public class RetryTests
{
    [Fact]
    public async Task RetryFailedRecords_RePullsOnlyErroredKeys_AsChildRun()
    {
        var client = new Mock<IErpClient>();
        client
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-GOOD-1"),
                ErpRecordFactory.Item("SKU-BAD-1", quantityOnHand: "NOPE"),
                ErpRecordFactory.Item("SKU-GOOD-2"),
            });
        client
            .Setup(c => c.GetInventoryBySkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-BAD-1", quantityOnHand: "42"),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db, client.Object);

        var parent = await service.SyncInventoryAsync("unit-test");
        parent.Status.Should().Be(SyncRunStatus.PartialSuccess);
        parent.RecordsFailed.Should().Be(1);

        var child = await service.RetryFailedRecordsAsync(parent.Id);

        child.ParentSyncRunId.Should().Be(parent.Id);
        child.Status.Should().Be(SyncRunStatus.Succeeded);
        child.RecordsRead.Should().Be(1);
        child.RecordsInserted.Should().Be(1);
        child.RecordsFailed.Should().Be(0);

        db.SyncRuns.Should().HaveCount(2);
        db.InventoryItems.Select(i => i.Sku).Should().BeEquivalentTo(new[] { "SKU-GOOD-1", "SKU-GOOD-2", "SKU-BAD-1" });

        client.Verify(
            c => c.GetInventoryBySkusAsync(
                It.Is<IReadOnlyCollection<string>>(skus => skus.Count == 1 && skus.Single() == "SKU-BAD-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryFailedRecords_OnlyRetriesRecordsWithUsableKeys()
    {
        var client = new Mock<IErpClient>();
        client
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("", quantityOnHand: "5"), // missing SKU -> unknown key
                ErpRecordFactory.Item("SKU-BAD-1", quantityOnHand: "NOPE"),
                ErpRecordFactory.Item("", quantityOnHand: "7"),
            });
        client
            .Setup(c => c.GetInventoryBySkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Item("SKU-BAD-1", quantityOnHand: "42"),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db, client.Object);

        var parent = await service.SyncInventoryAsync("unit-test");
        parent.RecordsFailed.Should().Be(3);

        var child = await service.RetryFailedRecordsAsync(parent.Id);

        child.RecordsRead.Should().Be(1);
        child.RecordsFailed.Should().Be(0);

        client.Verify(
            c => c.GetInventoryBySkusAsync(
                It.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(new[] { "SKU-BAD-1" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryFailedRecords_NoErrors_CompletesEmptyChildRun()
    {
        var client = new Mock<IErpClient>();
        client
            .Setup(c => c.GetInventoryAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ErpRecordFactory.Item("SKU-GOOD-1") });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db, client.Object);

        var parent = await service.SyncInventoryAsync("unit-test");
        parent.Status.Should().Be(SyncRunStatus.Succeeded);

        var child = await service.RetryFailedRecordsAsync(parent.Id);

        child.ParentSyncRunId.Should().Be(parent.Id);
        child.Status.Should().Be(SyncRunStatus.Succeeded);
        child.RecordsRead.Should().Be(0);
        db.SyncRuns.Should().HaveCount(2);
    }

    [Fact]
    public async Task RetryFailedRecords_PurchaseOrders_RePullsPoNumbers()
    {
        var client = new Mock<IErpClient>();
        client
            .Setup(c => c.GetPurchaseOrdersAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Order("PO-BAD-1", status: "WAT"),
                ErpRecordFactory.Order("PO-GOOD-1", status: "DRF"),
            });
        client
            .Setup(c => c.GetPurchaseOrdersByNumbersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErpRecordFactory.Order("PO-BAD-1", status: "DRF"),
            });

        await using var db = InMemoryDb.Create();
        var service = CreateService(db, client.Object);

        var parent = await service.SyncPurchaseOrdersAsync("unit-test");
        parent.RecordsFailed.Should().Be(1);

        var child = await service.RetryFailedRecordsAsync(parent.Id);

        child.EntityType.Should().Be(SyncEntityType.PurchaseOrder);
        child.RecordsInserted.Should().Be(1);
        client.Verify(
            c => c.GetPurchaseOrdersByNumbersAsync(
                It.Is<IReadOnlyCollection<string>>(keys => keys.SequenceEqual(new[] { "PO-BAD-1" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SyncService CreateService(SyncDbContext db, IErpClient client)
    {
        var options = Options.Create(new SyncOptions
        {
            PageSize = 10,
            ConflictResolution = ConflictResolutionStrategy.NewerWins,
        });

        return new SyncService(
            new InventoryItemRepository(db),
            new PurchaseOrderRepository(db),
            new SyncRepository(db),
            client,
            options);
    }
}
