using Microsoft.Extensions.Options;
using MockErp.Options;

namespace MockErp.Erp;

public class ErpStore
{
    private readonly IReadOnlyList<ErpItemRecord> _items;
    private readonly Dictionary<string, ErpItemRecord> _itemsBySku;
    private readonly IReadOnlyList<ErpOrderRecord> _orders;
    private readonly Dictionary<string, ErpOrderRecord> _ordersByNumber;

    public ErpStore(IOptions<MockErpOptions> options)
    {
        var erpOptions = options.Value;
        _items = ErpDataSource.GenerateItems(erpOptions.InventoryCount, erpOptions.Seed, erpOptions.SkuOffset);
        _itemsBySku = _items.ToDictionary(i => i.Sku, StringComparer.Ordinal);
        _orders = ErpDataSource.GenerateOrders(erpOptions.PurchaseOrderCount, erpOptions.Seed, erpOptions.InventoryCount, erpOptions.SkuOffset, erpOptions.PoNumberOffset);
        _ordersByNumber = _orders.ToDictionary(o => o.PoNumber, StringComparer.Ordinal);
    }

    public ErpItemRecord? FindItem(string sku)
    {
        return _itemsBySku.TryGetValue(sku, out var item) ? item : null;
    }

    public IReadOnlyList<ErpItemRecord> GetItems(DateTime? modifiedSince, IEnumerable<string>? skus)
    {
        IEnumerable<ErpItemRecord> filtered = _items;

        if (modifiedSince.HasValue)
        {
            filtered = filtered.Where(i => ErpDateFormats.TryParse(i.ModifiedDtm, out var modified) && modified >= modifiedSince.Value);
        }

        if (skus is not null)
        {
            var set = new HashSet<string>(skus, StringComparer.Ordinal);
            filtered = filtered.Where(i => set.Contains(i.Sku));
        }

        return filtered.ToList();
    }

    public IReadOnlyList<ErpOrderRecord> GetOrders(DateTime? modifiedSince, IEnumerable<string>? poNumbers)
    {
        IEnumerable<ErpOrderRecord> filtered = _orders;

        if (modifiedSince.HasValue)
        {
            filtered = filtered.Where(o => ErpDateFormats.TryParse(o.ModifiedDtm, out var modified) && modified >= modifiedSince.Value);
        }

        if (poNumbers is not null)
        {
            var set = new HashSet<string>(poNumbers, StringComparer.Ordinal);
            filtered = filtered.Where(o => set.Contains(o.PoNumber));
        }

        return filtered.ToList();
    }
}
