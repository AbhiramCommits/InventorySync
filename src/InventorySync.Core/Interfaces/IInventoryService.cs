using InventorySync.Core.Dtos;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Business operations for inventory items.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    Task<PagedResult<InventoryItemDto>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    Task<InventoryItemDto> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets an item by SKU.
    /// </summary>
    Task<InventoryItemDto> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// create async.
    /// </summary>
    Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    Task<InventoryItemDto> UpdateAsync(int id, UpdateInventoryItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// Marks a record as deleted.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}
