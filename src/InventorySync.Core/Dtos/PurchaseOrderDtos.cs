using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

/// <summary>
/// Purchase order data returned by the API.
/// </summary>
public class PurchaseOrderDto
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
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

/// <summary>
/// Purchase order line data returned by the API.
/// </summary>
public class PurchaseOrderLineDto
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

/// <summary>
/// Payload for creating a purchase order.
/// </summary>
public class CreatePurchaseOrderRequest
{
    /// <summary>
    /// Gets or sets the po number.
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vendor code.
    /// </summary>
    public string VendorCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order date utc.
    /// </summary>
    public DateTime OrderDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the expected date utc.
    /// </summary>
    public DateTime ExpectedDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// A single line in a purchase order creation payload.
/// </summary>
public class CreatePurchaseOrderLineRequest
{
    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity ordered.
    /// </summary>
    public int QuantityOrdered { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Payload for recording receipt of stock against an order line.
/// </summary>
public class ReceiveLineRequest
{
    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; }
}
