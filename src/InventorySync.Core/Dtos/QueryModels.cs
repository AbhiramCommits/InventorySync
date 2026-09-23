using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

public class InventoryItemQuery
{
    public string? Search { get; set; }

    public string? WarehouseCode { get; set; }

    public DateTime? LastSyncedFrom { get; set; }

    public DateTime? LastSyncedTo { get; set; }

    public string Sort { get; set; } = "sku";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class PurchaseOrderQuery
{
    public string? Search { get; set; }

    public string? VendorCode { get; set; }

    public PurchaseOrderStatus? Status { get; set; }

    public DateTime? OrderDateFrom { get; set; }

    public DateTime? OrderDateTo { get; set; }

    public string Sort { get; set; } = "poNumber";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
