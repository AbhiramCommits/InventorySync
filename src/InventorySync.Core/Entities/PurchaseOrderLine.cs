namespace InventorySync.Core.Entities;

/// <summary>
/// Represents a single line on a purchase order.
/// </summary>
public class PurchaseOrderLine
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the purchase order id.
    /// </summary>
    public int PurchaseOrderId { get; set; }

    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity ordered.
    /// </summary>
    public int QuantityOrdered { get; set; }

    /// <summary>
    /// Gets or sets the quantity received.
    /// </summary>
    public int QuantityReceived { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }
}
