using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

/// <summary>
/// Persistence operations for sync runs and audit entries.
/// </summary>
public interface ISyncRepository
{
    /// <summary>
    /// add run async.
    /// </summary>
    Task AddRunAsync(SyncRun run, CancellationToken ct = default);

    /// <summary>
    /// Gets sync runs.
    /// </summary>
    Task<PagedResult<SyncRun>> GetRunsAsync(SyncEntityType? entityType, SyncRunStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Gets a sync run by id.
    /// </summary>
    Task<SyncRun?> GetRunByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets audit entries.
    /// </summary>
    Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesAsync(int syncRunId, SyncAuditAction? action, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// count audit entries async.
    /// </summary>
    Task<int> CountAuditEntriesAsync(int syncRunId, SyncAuditAction? action, CancellationToken ct = default);

    /// <summary>
    /// Gets audit entries.
    /// </summary>
    Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesByActionAsync(int syncRunId, SyncAuditAction action, CancellationToken ct = default);

    /// <summary>
    /// Stages audit entries for saving.
    /// </summary>
    void AddAuditEntries(IEnumerable<SyncAuditEntry> entries);

    /// <summary>
    /// save changes async.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
