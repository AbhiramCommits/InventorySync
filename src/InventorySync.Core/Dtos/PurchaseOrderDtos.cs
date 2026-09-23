using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

public class PurchaseOrderDto
{
    public int Id { get; set; }

    public string PoNumber { get; set; } = string.Empty;

    public string VendorCode { get; set; } = string.Empty;

    public PurchaseOrderStatus Status { get; set; }

    public DateTime OrderDateUtc { get; set; }

    public DateTime ExpectedDateUtc { get; set; }

    public decimal TotalAmount { get; set; }

    public string? ErpRecordId { get; set; }

    public DateTime? LastSyncedUtc { get; set; }

    public DateTime? LocallyModifiedUtc { get; set; }

    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

public class PurchaseOrderLineDto
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int QuantityOrdered { get; set; }

    public int QuantityReceived { get; set; }

    public decimal UnitPrice { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public string PoNumber { get; set; } = string.Empty;

    public string VendorCode { get; set; } = string.Empty;

    public DateTime OrderDateUtc { get; set; }

    public DateTime ExpectedDateUtc { get; set; }

    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
}

public class CreatePurchaseOrderLineRequest
{
    public string Sku { get; set; } = string.Empty;

    public int QuantityOrdered { get; set; }

    public decimal UnitPrice { get; set; }
}

public class ReceiveLineRequest
{
    public int Quantity { get; set; }
}
