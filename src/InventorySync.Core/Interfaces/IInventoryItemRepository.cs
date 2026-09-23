using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;

namespace InventorySync.Core.Interfaces;

public interface IInventoryItemRepository
{
    Task<PagedResult<InventoryItem>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default);

    Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default);

    Task<Dictionary<string, InventoryItem>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default);

    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);

    Task AddAsync(InventoryItem item, CancellationToken ct = default);

    void Update(InventoryItem item);

    void Delete(InventoryItem item);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
