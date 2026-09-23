namespace InventorySync.Core.Entities;

public class InventoryItem
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityOnHand { get; set; }

    public decimal UnitCost { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public DateTime? LastSyncedUtc { get; set; }

    public DateTime? LocallyModifiedUtc { get; set; }

    public string? ErpRecordId { get; set; }

    public byte[]? RowVersion { get; set; }
}
