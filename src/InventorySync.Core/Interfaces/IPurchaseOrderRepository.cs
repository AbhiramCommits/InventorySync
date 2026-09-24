using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Persistence operations for purchase orders.
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    Task<PagedResult<PurchaseOrder>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    Task<PurchaseOrder?> GetByIdAsync(int id, bool includeLines, CancellationToken ct = default);

    /// <summary>
    /// Gets orders by PO number.
    /// </summary>
    Task<Dictionary<string, PurchaseOrder>> GetByPoNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default);

    /// <summary>
    /// po number exists async.
    /// </summary>
    Task<bool> PoNumberExistsAsync(string poNumber, CancellationToken ct = default);

    /// <summary>
    /// add async.
    /// </summary>
    Task AddAsync(PurchaseOrder order, CancellationToken ct = default);

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    void Update(PurchaseOrder order);

    /// <summary>
    /// Removes purchase order lines.
    /// </summary>
    void RemoveLines(IEnumerable<PurchaseOrderLine> lines);

    /// <summary>
    /// save changes async.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
