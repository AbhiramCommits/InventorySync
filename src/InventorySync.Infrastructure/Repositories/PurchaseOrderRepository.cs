using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the purchase order repository.
/// </summary>
public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["poNumber"] = nameof(PurchaseOrder.PoNumber),
            ["vendorCode"] = nameof(PurchaseOrder.VendorCode),
            ["status"] = nameof(PurchaseOrder.Status),
            ["orderDateUtc"] = nameof(PurchaseOrder.OrderDateUtc),
            ["expectedDateUtc"] = nameof(PurchaseOrder.ExpectedDateUtc),
            ["totalAmount"] = nameof(PurchaseOrder.TotalAmount),
        };

    private readonly SyncDbContext _context;

    /// <summary>
    /// Initializes a new instance of the PurchaseOrderRepository class.
    /// </summary>
    public PurchaseOrderRepository(SyncDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    public async Task<PagedResult<PurchaseOrder>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken ct = default)
    {
        var filtered = _context.PurchaseOrders.AsNoTracking();

        if (query.IncludeLines)
        {
            filtered = filtered.Include(x => x.Lines);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(x => x.PoNumber.Contains(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.VendorCode))
        {
            filtered = filtered.Where(x => x.VendorCode == query.VendorCode);
        }

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(x => x.Status == query.Status.Value);
        }

        if (query.OrderDateFrom.HasValue)
        {
            filtered = filtered.Where(x => x.OrderDateUtc >= query.OrderDateFrom.Value);
        }

        if (query.OrderDateTo.HasValue)
        {
            filtered = filtered.Where(x => x.OrderDateUtc <= query.OrderDateTo.Value);
        }

        var totalCount = await filtered.CountAsync(ct);
        var items = await ApplySorting(filtered, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PurchaseOrder>
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
    public Task<PurchaseOrder?> GetByIdAsync(int id, bool includeLines, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders.AsQueryable();

        if (includeLines)
        {
            query = query.Include(x => x.Lines);
        }

        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Gets orders by PO number.
    /// </summary>
    public async Task<Dictionary<string, PurchaseOrder>> GetByPoNumbersAsync(
        IReadOnlyCollection<string> poNumbers,
        CancellationToken ct = default)
    {
        var orders = await _context.PurchaseOrders
            .Include(x => x.Lines)
            .Where(x => poNumbers.Contains(x.PoNumber))
            .ToListAsync(ct);

        return orders.ToDictionary(x => x.PoNumber, StringComparer.Ordinal);
    }

    /// <summary>
    /// po number exists async.
    /// </summary>
    public Task<bool> PoNumberExistsAsync(string poNumber, CancellationToken ct = default)
    {
        return _context.PurchaseOrders.AnyAsync(x => x.PoNumber == poNumber, ct);
    }

    /// <summary>
    /// add async.
    /// </summary>
    public Task AddAsync(PurchaseOrder order, CancellationToken ct = default)
    {
        return _context.PurchaseOrders.AddAsync(order, ct).AsTask();
    }

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    public void Update(PurchaseOrder order)
    {
        _context.PurchaseOrders.Update(order);
    }

    /// <summary>
    /// Removes purchase order lines.
    /// </summary>
    public void RemoveLines(IEnumerable<PurchaseOrderLine> lines)
    {
        _context.PurchaseOrderLines.RemoveRange(lines);
    }

    /// <summary>
    /// save changes async.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    private static IQueryable<PurchaseOrder> ApplySorting(IQueryable<PurchaseOrder> query, string sort)
    {
        var descending = sort.StartsWith('-');
        var column = descending ? sort[1..] : sort;

        if (!SortColumns.TryGetValue(column, out var property))
        {
            property = nameof(PurchaseOrder.PoNumber);
        }

        return descending
            ? query.OrderByDescending(x => EF.Property<object>(x, property))
            : query.OrderBy(x => EF.Property<object>(x, property));
    }
}
