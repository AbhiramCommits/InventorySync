using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Interfaces;

namespace InventorySync.Tests.TestHelpers;

internal sealed class FakeErpClient : IErpClient
{
    private readonly List<ErpInventoryItemRecord> _items;
    private readonly List<ErpPurchaseOrderRecord> _orders;

    public FakeErpClient(params ErpInventoryItemRecord[] items)
        : this(items, Array.Empty<ErpPurchaseOrderRecord>())
    {
    }

    public FakeErpClient(IEnumerable<ErpPurchaseOrderRecord>? orders)
        : this(null, orders)
    {
    }

    private FakeErpClient(IEnumerable<ErpInventoryItemRecord>? items, IEnumerable<ErpPurchaseOrderRecord>? orders)
    {
        _items = items?.ToList() ?? new List<ErpInventoryItemRecord>();
        _orders = orders?.ToList() ?? new List<ErpPurchaseOrderRecord>();
    }

    public IReadOnlyList<string>? LastRequestedSkus { get; private set; }

    public IReadOnlyList<string>? LastRequestedPoNumbers { get; private set; }

    public void ReplaceItem(string sku, ErpInventoryItemRecord replacement)
    {
        var index = _items.FindIndex(i => i.Sku == sku);
        if (index >= 0)
        {
            _items[index] = replacement;
        }
    }

    public void ReplaceOrder(string poNumber, ErpPurchaseOrderRecord replacement)
    {
        var index = _orders.FindIndex(o => o.PoNumber == poNumber);
        if (index >= 0)
        {
            _orders[index] = replacement;
        }
    }

    public Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ErpInventoryItemRecord>>(_items.ToList());
    }

    public Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default)
    {
        LastRequestedSkus = skus.ToList();
        var set = new HashSet<string>(skus, StringComparer.Ordinal);
        var result = _items.Where(i => i.Sku is not null && set.Contains(i.Sku)).ToList();
        return Task.FromResult<IReadOnlyList<ErpInventoryItemRecord>>(result);
    }

    public Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ErpPurchaseOrderRecord>>(_orders.ToList());
    }

    public Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersByNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default)
    {
        LastRequestedPoNumbers = poNumbers.ToList();
        var set = new HashSet<string>(poNumbers, StringComparer.Ordinal);
        var result = _orders.Where(o => o.PoNumber is not null && set.Contains(o.PoNumber)).ToList();
        return Task.FromResult<IReadOnlyList<ErpPurchaseOrderRecord>>(result);
    }

    public Task<ErpItemDetailRecord?> GetItemDetailAsync(string sku, CancellationToken ct = default)
    {
        return Task.FromResult<ErpItemDetailRecord?>(null);
    }
}
