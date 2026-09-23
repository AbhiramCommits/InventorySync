using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly SyncDbContext _context;

    public PurchaseOrderRepository(SyncDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PurchaseOrder>> GetPagedAsync(
        PurchaseOrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.PoNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PurchaseOrder>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
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
}
