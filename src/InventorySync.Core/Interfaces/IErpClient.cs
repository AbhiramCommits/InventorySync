using InventorySync.Core.Dtos.Erp;

namespace InventorySync.Core.Interfaces;

public interface IErpClient
{
    Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default);

    Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default);

    Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default);

    Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersByNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default);

    Task<ErpItemDetailRecord?> GetItemDetailAsync(string sku, CancellationToken ct = default);
}
