using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

public class InventoryItemRepository : IInventoryItemRepository
{
    private readonly SyncDbContext _context;

    public InventoryItemRepository(SyncDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<InventoryItem>> GetPagedAsync(
        string? warehouseCode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.InventoryItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            query = query.Where(x => x.WarehouseCode == warehouseCode);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InventoryItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        return _context.InventoryItems.FirstOrDefaultAsync(x => x.Sku == sku, ct);
    }

    public async Task<Dictionary<string, InventoryItem>> GetBySkusAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken ct = default)
    {
        var items = await _context.InventoryItems
            .Where(x => skus.Contains(x.Sku))
            .ToListAsync(ct);

        return items.ToDictionary(x => x.Sku, StringComparer.Ordinal);
    }

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
    {
        return _context.InventoryItems.AnyAsync(x => x.Sku == sku, ct);
    }

    public Task AddAsync(InventoryItem item, CancellationToken ct = default)
    {
        return _context.InventoryItems.AddAsync(item, ct).AsTask();
    }

    public void Update(InventoryItem item)
    {
        _context.InventoryItems.Update(item);
    }

    public void Delete(InventoryItem item)
    {
        _context.InventoryItems.Remove(item);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
