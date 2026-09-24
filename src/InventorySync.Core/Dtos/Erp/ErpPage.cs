namespace InventorySync.Core.Dtos.Erp;

/// <summary>
/// Page envelope returned by the mock ERP REST API.
/// </summary>
public sealed class ErpPage<T>
{
    /// <summary>
    /// Gets or sets the items.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the page.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count.
    /// </summary>
    public int TotalCount { get; set; }
}
