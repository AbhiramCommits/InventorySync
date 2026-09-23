using InventorySync.Core.Dtos;

namespace InventorySync.Core.Interfaces;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetPagedAsync(string? warehouseCode, int page, int pageSize, CancellationToken ct = default);

    Task<InventoryItemDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<InventoryItemDto> GetBySkuAsync(string sku, CancellationToken ct = default);

    Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken ct = default);

    Task<InventoryItemDto> UpdateAsync(int id, UpdateInventoryItemRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
