using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Orchestrates synchronisation runs, retries and audit queries.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Runs a full inventory synchronisation.
    /// </summary>
    Task<SyncRunDto> SyncInventoryAsync(string triggeredBy, CancellationToken ct = default);

    /// <summary>
    /// Runs a full purchase order synchronisation.
    /// </summary>
    Task<SyncRunDto> SyncPurchaseOrdersAsync(string triggeredBy, CancellationToken ct = default);

    /// <summary>
    /// Retries the records that failed in a run.
    /// </summary>
    Task<SyncRunDto> RetryFailedRecordsAsync(int syncRunId, CancellationToken ct = default);

    /// <summary>
    /// Gets sync runs.
    /// </summary>
    Task<PagedResult<SyncRunDto>> GetRunsAsync(SyncEntityType? entityType, SyncRunStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Gets a sync run by id.
    /// </summary>
    Task<SyncRunDto> GetRunByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets audit entries.
    /// </summary>
    Task<PagedResult<SyncAuditEntryDto>> GetAuditEntriesAsync(int syncRunId, SyncAuditAction? action, int page, int pageSize, CancellationToken ct = default);
}
