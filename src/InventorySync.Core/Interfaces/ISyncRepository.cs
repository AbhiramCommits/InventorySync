using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;

namespace InventorySync.Core.Interfaces;

public interface ISyncRepository
{
    Task AddRunAsync(SyncRun run, CancellationToken ct = default);

    Task<PagedResult<SyncRun>> GetRunsAsync(SyncEntityType? entityType, SyncRunStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<SyncRun?> GetRunByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesAsync(int syncRunId, SyncAuditAction? action, int skip, int take, CancellationToken ct = default);

    Task<int> CountAuditEntriesAsync(int syncRunId, SyncAuditAction? action, CancellationToken ct = default);

    Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesByActionAsync(int syncRunId, SyncAuditAction action, CancellationToken ct = default);

    void AddAuditEntries(IEnumerable<SyncAuditEntry> entries);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
