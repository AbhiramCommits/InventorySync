using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;

namespace InventorySync.Core.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<PagedResult<PurchaseOrder>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken ct = default);

    Task<PurchaseOrder?> GetByIdAsync(int id, bool includeLines, CancellationToken ct = default);

    Task<Dictionary<string, PurchaseOrder>> GetByPoNumbersAsync(IReadOnlyCollection<string> poNumbers, CancellationToken ct = default);

    Task<bool> PoNumberExistsAsync(string poNumber, CancellationToken ct = default);

    Task AddAsync(PurchaseOrder order, CancellationToken ct = default);

    void Update(PurchaseOrder order);

    void RemoveLines(IEnumerable<PurchaseOrderLine> lines);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
