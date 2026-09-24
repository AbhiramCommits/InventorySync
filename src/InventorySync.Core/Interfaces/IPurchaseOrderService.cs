using InventorySync.Core.Dtos;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Business operations for purchase orders.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    Task<PurchaseOrderDto> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// create async.
    /// </summary>
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// submit async.
    /// </summary>
    Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// receive line async.
    /// </summary>
    Task<PurchaseOrderDto> ReceiveLineAsync(int id, int lineId, ReceiveLineRequest request, CancellationToken ct = default);

    /// <summary>
    /// cancel async.
    /// </summary>
    Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct = default);
}
