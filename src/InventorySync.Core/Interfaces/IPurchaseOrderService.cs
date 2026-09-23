using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<PurchaseOrderDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);

    Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct = default);

    Task<PurchaseOrderDto> ReceiveLineAsync(int id, int lineId, ReceiveLineRequest request, CancellationToken ct = default);

    Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct = default);
}
