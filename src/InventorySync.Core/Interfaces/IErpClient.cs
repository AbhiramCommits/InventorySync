using InventorySync.Core.Dtos.Erp;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Client abstraction over the ERP system's REST and SOAP endpoints.
/// </summary>
public interface IErpClient
{
    /// <summary>
    /// Gets inventory records from the ERP.
    /// </summary>
    Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default);

    /// <summary>
    /// Gets inventory records from the ERP.
    /// </summary>
    Task<IReadOnlyList<ErpInventoryItemRecord>> GetInventoryBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default);

    /// <summary>
    /// Gets purchase order records from the ERP.
    /// </summary>
    Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default);

    /// <summary>
    /// Gets purchase order records from the ERP.
    /// </summary>
    Task<IReadOnlyList<ErpPurchaseOrderRecord>> GetPurchaseOrdersByNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default);

    /// <summary>
    /// Gets an item detail from the ERP SOAP endpoint.
    /// </summary>
    Task<ErpItemDetailRecord?> GetItemDetailAsync(string sku, CancellationToken ct = default);
}
