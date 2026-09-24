using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the sync run repository.
/// </summary>
public class SyncRepository : ISyncRepository
{
    private readonly SyncDbContext _context;

    /// <summary>
    /// Initializes a new instance of the SyncRepository class.
    /// </summary>
    public SyncRepository(SyncDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// add run async.
    /// </summary>
    public Task AddRunAsync(SyncRun run, CancellationToken ct = default)
    {
        return _context.SyncRuns.AddAsync(run, ct).AsTask();
    }

    /// <summary>
    /// Gets sync runs.
    /// </summary>
    public async Task<PagedResult<SyncRun>> GetRunsAsync(
        SyncEntityType? entityType,
        SyncRunStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.SyncRuns.AsNoTracking();

        if (entityType.HasValue)
        {
            query = query.Where(x => x.EntityType == entityType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.StartedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SyncRun>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Gets a sync run by id.
    /// </summary>
    public Task<SyncRun?> GetRunByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.SyncRuns.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Gets audit entries.
    /// </summary>
    public async Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesAsync(
        int syncRunId,
        SyncAuditAction? action,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.SyncAuditEntries
            .AsNoTracking()
            .Where(x => x.SyncRunId == syncRunId);

        if (action.HasValue)
        {
            query = query.Where(x => x.Action == action.Value);
        }

        return await query
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// count audit entries async.
    /// </summary>
    public Task<int> CountAuditEntriesAsync(int syncRunId, SyncAuditAction? action, CancellationToken ct = default)
    {
        var query = _context.SyncAuditEntries.AsNoTracking().Where(x => x.SyncRunId == syncRunId);

        if (action.HasValue)
        {
            query = query.Where(x => x.Action == action.Value);
        }

        return query.CountAsync(ct);
    }

    /// <summary>
    /// Gets audit entries.
    /// </summary>
    public async Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesByActionAsync(
        int syncRunId,
        SyncAuditAction action,
        CancellationToken ct = default)
    {
        return await _context.SyncAuditEntries
            .AsNoTracking()
            .Where(x => x.SyncRunId == syncRunId && x.Action == action)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Stages audit entries for saving.
    /// </summary>
    public void AddAuditEntries(IEnumerable<SyncAuditEntry> entries)
    {
        _context.SyncAuditEntries.AddRange(entries);
    }

    /// <summary>
    /// save changes async.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
