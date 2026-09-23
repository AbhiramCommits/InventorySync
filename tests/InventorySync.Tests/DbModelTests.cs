using InventorySync.Core.Entities;
using InventorySync.Tests.TestHelpers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InventorySync.Tests;

public class DbModelTests
{
    [Fact]
    public void InventoryItem_HasUniqueSkuIndex()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(InventoryItem))!;

        var skuIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(InventoryItem.Sku)) && i.Properties.Count == 1);
        Assert.True(skuIndex.IsUnique);
    }

    [Fact]
    public void InventoryItem_HasWarehouseLastSyncedIndex()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(InventoryItem))!;

        var index = entity.GetIndexes().Single(i =>
            i.Properties.Count == 2
            && i.Properties[0].Name == nameof(InventoryItem.WarehouseCode)
            && i.Properties[1].Name == nameof(InventoryItem.LastSyncedUtc));

        Assert.False(index.IsUnique);
    }

    [Fact]
    public void PurchaseOrder_HasUniquePoNumberIndex()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(PurchaseOrder))!;

        var poIndex = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(PurchaseOrder.PoNumber)) && i.Properties.Count == 1);
        Assert.True(poIndex.IsUnique);
    }

    [Fact]
    public void SyncAuditEntry_HasCompositeSyncRunEntityTypeIndex()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(SyncAuditEntry))!;

        var index = entity.GetIndexes().Single(i =>
            i.Properties.Count == 2
            && i.Properties[0].Name == nameof(SyncAuditEntry.SyncRunId)
            && i.Properties[1].Name == nameof(SyncAuditEntry.EntityType));

        Assert.False(index.IsUnique);
    }

    [Fact]
    public void InventoryItem_UnitCost_HasPrecision18Scale4()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(InventoryItem))!;
        var property = entity.FindProperty(nameof(InventoryItem.UnitCost))!;

        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(4, property.GetScale());
    }

    [Fact]
    public void PurchaseOrder_TotalAmount_HasPrecision18Scale2()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(PurchaseOrder))!;
        var property = entity.FindProperty(nameof(PurchaseOrder.TotalAmount))!;

        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public void InventoryItem_RowVersion_IsConcurrencyToken()
    {
        using var db = InMemoryDb.Create();
        var entity = db.Model.FindEntityType(typeof(InventoryItem))!;
        var property = entity.FindProperty(nameof(InventoryItem.RowVersion))!;

        Assert.True(property.IsConcurrencyToken);
    }

    [Fact]
    public void ReportingIndexes_AreConfiguredWithFiltersAndIncludes()
    {
        using var db = InMemoryDb.Create();
        var model = db.GetService<IDesignTimeModel>().Model;

        var lowStock = model.FindEntityType(typeof(InventoryItem))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "IX_InventoryItems_LowStock");
        Assert.Equal("QuantityOnHand < 25", lowStock.GetFilter());

        var valuation = model.FindEntityType(typeof(InventoryItem))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "IX_InventoryItems_Whse_Valuation");
        Assert.Contains(nameof(InventoryItem.QuantityOnHand), valuation.GetIncludeProperties()!);
        Assert.Contains(nameof(InventoryItem.UnitCost), valuation.GetIncludeProperties()!);

        var openPo = model.FindEntityType(typeof(PurchaseOrder))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "IX_PurchaseOrders_OpenByStatus");
        Assert.Equal("Status IN (1, 2)", openPo.GetFilter());

        var openLines = model.FindEntityType(typeof(PurchaseOrderLine))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "IX_PurchaseOrderLines_Sku_Open");
        Assert.Null(openLines.GetFilter());
        Assert.Contains(nameof(PurchaseOrderLine.QuantityOrdered), openLines.GetIncludeProperties()!);
        Assert.Contains(nameof(PurchaseOrderLine.QuantityReceived), openLines.GetIncludeProperties()!);

        var auditAction = model.FindEntityType(typeof(SyncAuditEntry))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "IX_SyncAuditEntries_Action_TimestampUtc");
        Assert.Null(auditAction.GetFilter());
        Assert.Contains(nameof(SyncAuditEntry.SyncRunId), auditAction.GetIncludeProperties()!);
    }
}
