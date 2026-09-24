using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Persistence operations for inventory items.
/// </summary>
public interface IInventoryItemRepository
{
    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    Task<PagedResult<InventoryItem>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets an item by SKU.
    /// </summary>
    Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// Gets items by their SKUs.
    /// </summary>
    Task<Dictionary<string, InventoryItem>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default);

    /// <summary>
    /// sku exists async.
    /// </summary>
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// add async.
    /// </summary>
    Task AddAsync(InventoryItem item, CancellationToken ct = default);

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    void Update(InventoryItem item);

    /// <summary>
    /// Sets the original rowversion used for optimistic concurrency.
    /// </summary>
    void SetOriginalRowVersion(InventoryItem item, byte[] rowVersion);

    /// <summary>
    /// Marks a record as deleted.
    /// </summary>
    void Delete(InventoryItem item);

    /// <summary>
    /// save changes async.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
