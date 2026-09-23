using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

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

    public PurchaseOrderRepository(SyncDbContext context)
    {
        _context = context;
    }

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

    public Task<PurchaseOrder?> GetByIdAsync(int id, bool includeLines, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders.AsQueryable();

        if (includeLines)
        {
            query = query.Include(x => x.Lines);
        }

        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

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

    public Task<bool> PoNumberExistsAsync(string poNumber, CancellationToken ct = default)
    {
        return _context.PurchaseOrders.AnyAsync(x => x.PoNumber == poNumber, ct);
    }

    public Task AddAsync(PurchaseOrder order, CancellationToken ct = default)
    {
        return _context.PurchaseOrders.AddAsync(order, ct).AsTask();
    }

    public void Update(PurchaseOrder order)
    {
        _context.PurchaseOrders.Update(order);
    }

    public void RemoveLines(IEnumerable<PurchaseOrderLine> lines)
    {
        _context.PurchaseOrderLines.RemoveRange(lines);
    }

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
