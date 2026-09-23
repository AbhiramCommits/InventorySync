using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

public interface ISyncService
{
    Task<SyncResultDto> SyncInventoryAsync(string triggeredBy, CancellationToken ct = default);

    Task<SyncResultDto> SyncPurchaseOrdersAsync(string triggeredBy, CancellationToken ct = default);

    Task<PagedResult<SyncRunDto>> GetRunsAsync(SyncEntityType? entityType, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<SyncAuditEntryDto>> GetAuditEntriesAsync(int syncRunId, int page, int pageSize, CancellationToken ct = default);
}
