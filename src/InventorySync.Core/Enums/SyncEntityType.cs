namespace InventorySync.Core.Enums;

/// <summary>
/// The kind of entity a sync run operates on.
/// </summary>
public enum SyncEntityType
{
    /// <summary>
    /// Represents the inventory item option.
    /// </summary>
    InventoryItem = 0,
    /// <summary>
    /// Represents the purchase order option.
    /// </summary>
    PurchaseOrder = 1,
}
