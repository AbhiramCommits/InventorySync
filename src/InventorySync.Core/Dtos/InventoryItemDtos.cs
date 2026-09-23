using System.Text.Json.Serialization;

namespace InventorySync.Core.Dtos;

public class InventoryItemDto
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityOnHand { get; set; }

    public decimal UnitCost { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public DateTime? LastSyncedUtc { get; set; }

    public string? ErpRecordId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RowVersion { get; set; }
}

public class CreateInventoryItemRequest
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityOnHand { get; set; }

    public decimal UnitCost { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;
}

public class UpdateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityOnHand { get; set; }

    public decimal UnitCost { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}
