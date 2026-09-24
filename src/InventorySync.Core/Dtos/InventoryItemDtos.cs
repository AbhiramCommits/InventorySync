using System.Text.Json.Serialization;

namespace InventorySync.Core.Dtos;

/// <summary>
/// Inventory item data returned by the API.
/// </summary>
public class InventoryItemDto
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    public int QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last synced utc.
    /// </summary>
    public DateTime? LastSyncedUtc { get; set; }

    /// <summary>
    /// Gets or sets the locally modified utc.
    /// </summary>
    public DateTime? LocallyModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the erp record id.
    /// </summary>
    public string? ErpRecordId { get; set; }

    /// <summary>
    /// Gets or sets the row version.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RowVersion { get; set; }
}

/// <summary>
/// Payload for creating an inventory item.
/// </summary>
public class CreateInventoryItemRequest
{
    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    public int QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    public string WarehouseCode { get; set; } = string.Empty;
}

/// <summary>
/// Payload for updating an inventory item.
/// </summary>
public class UpdateInventoryItemRequest
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    public int QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row version.
    /// </summary>
    public string? RowVersion { get; set; }
}
