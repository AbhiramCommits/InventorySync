using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

/// <summary>
/// Filters, sorting and paging options for inventory list queries.
/// </summary>
public class InventoryItemQuery
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    public string? WarehouseCode { get; set; }

    /// <summary>
    /// Gets or sets the last synced from.
    /// </summary>
    public DateTime? LastSyncedFrom { get; set; }

    /// <summary>
    /// Gets or sets the last synced to.
    /// </summary>
    public DateTime? LastSyncedTo { get; set; }

    /// <summary>
    /// Gets or sets the sort.
    /// </summary>
    public string Sort { get; set; } = "sku";

    /// <summary>
    /// Gets or sets the page.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Filters, sorting and paging options for purchase order list queries.
/// </summary>
public class PurchaseOrderQuery
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the vendor code.
    /// </summary>
    public string? VendorCode { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public PurchaseOrderStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the order date from.
    /// </summary>
    public DateTime? OrderDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the order date to.
    /// </summary>
    public DateTime? OrderDateTo { get; set; }

    /// <summary>
    /// Gets or sets the sort.
    /// </summary>
    public string Sort { get; set; } = "poNumber";

    /// <summary>
    /// Gets or sets the page.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the include lines.
    /// </summary>
    public bool IncludeLines { get; set; }
}
