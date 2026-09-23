namespace InventorySync.Core.Entities;

public class PurchaseOrderLine
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int QuantityOrdered { get; set; }

    public int QuantityReceived { get; set; }

    public decimal UnitPrice { get; set; }
}
