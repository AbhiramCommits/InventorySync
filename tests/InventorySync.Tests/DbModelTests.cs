using InventorySync.Core.Entities;
using InventorySync.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

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
}
