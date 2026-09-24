namespace InventorySync.Core.Enums;

/// <summary>
/// The lifecycle status of a purchase order.
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>
    /// Represents the draft option.
    /// </summary>
    Draft = 0,
    /// <summary>
    /// Represents the submitted option.
    /// </summary>
    Submitted = 1,
    /// <summary>
    /// Represents the partially received option.
    /// </summary>
    PartiallyReceived = 2,
    /// <summary>
    /// Represents the received option.
    /// </summary>
    Received = 3,
    /// <summary>
    /// Represents the cancelled option.
    /// </summary>
    Cancelled = 4,
}
