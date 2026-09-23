namespace InventorySync.Core.Dtos.Erp;

public sealed class ErpPage<T>
{
    public List<T> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}
