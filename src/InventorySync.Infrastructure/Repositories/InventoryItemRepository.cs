using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the inventory item repository.
/// </summary>
public class InventoryItemRepository : IInventoryItemRepository
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sku"] = nameof(InventoryItem.Sku),
            ["name"] = nameof(InventoryItem.Name),
            ["quantityOnHand"] = nameof(InventoryItem.QuantityOnHand),
            ["unitCost"] = nameof(InventoryItem.UnitCost),
            ["warehouseCode"] = nameof(InventoryItem.WarehouseCode),
            ["lastSyncedUtc"] = nameof(InventoryItem.LastSyncedUtc),
        };

    private readonly SyncDbContext _context;

    /// <summary>
    /// Initializes a new instance of the InventoryItemRepository class.
    /// </summary>
    public InventoryItemRepository(SyncDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    public async Task<PagedResult<InventoryItem>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default)
    {
        var filtered = _context.InventoryItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(x => x.Sku.Contains(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.WarehouseCode))
        {
            filtered = filtered.Where(x => x.WarehouseCode == query.WarehouseCode);
        }

        if (query.LastSyncedFrom.HasValue)
        {
            filtered = filtered.Where(x => x.LastSyncedUtc >= query.LastSyncedFrom.Value);
        }

        if (query.LastSyncedTo.HasValue)
        {
            filtered = filtered.Where(x => x.LastSyncedUtc <= query.LastSyncedTo.Value);
        }

        var totalCount = await filtered.CountAsync(ct);
        var items = await ApplySorting(filtered, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<InventoryItem>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    public Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Gets an item by SKU.
    /// </summary>
    public Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        return _context.InventoryItems.FirstOrDefaultAsync(x => x.Sku == sku, ct);
    }

    /// <summary>
    /// Gets items by their SKUs.
    /// </summary>
    public async Task<Dictionary<string, InventoryItem>> GetBySkusAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken ct = default)
    {
        var items = await _context.InventoryItems
            .Where(x => skus.Contains(x.Sku))
            .ToListAsync(ct);

        return items.ToDictionary(x => x.Sku, StringComparer.Ordinal);
    }

    /// <summary>
    /// sku exists async.
    /// </summary>
    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
    {
        return _context.InventoryItems.AnyAsync(x => x.Sku == sku, ct);
    }

    /// <summary>
    /// add async.
    /// </summary>
    public Task AddAsync(InventoryItem item, CancellationToken ct = default)
    {
        return _context.InventoryItems.AddAsync(item, ct).AsTask();
    }

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    public void Update(InventoryItem item)
    {
        _context.InventoryItems.Update(item);
    }

    /// <summary>
    /// Sets the original rowversion used for optimistic concurrency.
    /// </summary>
    public void SetOriginalRowVersion(InventoryItem item, byte[] rowVersion)
    {
        _context.Entry(item).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    /// <summary>
    /// Marks a record as deleted.
    /// </summary>
    public void Delete(InventoryItem item)
    {
        _context.InventoryItems.Remove(item);
    }

    /// <summary>
    /// save changes async.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    private static IQueryable<InventoryItem> ApplySorting(IQueryable<InventoryItem> query, string sort)
    {
        var descending = sort.StartsWith('-');
        var column = descending ? sort[1..] : sort;

        if (!SortColumns.TryGetValue(column, out var property))
        {
            property = nameof(InventoryItem.Sku);
        }

        return descending
            ? query.OrderByDescending(x => EF.Property<object>(x, property))
            : query.OrderBy(x => EF.Property<object>(x, property));
    }
}
