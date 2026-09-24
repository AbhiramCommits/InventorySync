using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

/// <summary>
/// Represents a purchase order and its line items.
/// </summary>
public class PurchaseOrder
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the po number.
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vendor code.
    /// </summary>
    public string VendorCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public PurchaseOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the order date utc.
    /// </summary>
    public DateTime OrderDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the expected date utc.
    /// </summary>
    public DateTime ExpectedDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the total amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the erp record id.
    /// </summary>
    public string? ErpRecordId { get; set; }

    /// <summary>
    /// Gets or sets the last synced utc.
    /// </summary>
    public DateTime? LastSyncedUtc { get; set; }

    /// <summary>
    /// Gets or sets the locally modified utc.
    /// </summary>
    public DateTime? LocallyModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
