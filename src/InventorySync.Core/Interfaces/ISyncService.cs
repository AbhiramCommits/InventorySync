using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

public interface ISyncService
{
    Task<SyncRunDto> SyncInventoryAsync(string triggeredBy, CancellationToken ct = default);

    Task<SyncRunDto> SyncPurchaseOrdersAsync(string triggeredBy, CancellationToken ct = default);

    Task<SyncRunDto> RetryFailedRecordsAsync(int syncRunId, CancellationToken ct = default);

    Task<PagedResult<SyncRunDto>> GetRunsAsync(SyncEntityType? entityType, SyncRunStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<SyncRunDto> GetRunByIdAsync(int id, CancellationToken ct = default);

    Task<PagedResult<SyncAuditEntryDto>> GetAuditEntriesAsync(int syncRunId, SyncAuditAction? action, int page, int pageSize, CancellationToken ct = default);
}
