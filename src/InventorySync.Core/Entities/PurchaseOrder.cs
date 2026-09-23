using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

public class PurchaseOrder
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

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
