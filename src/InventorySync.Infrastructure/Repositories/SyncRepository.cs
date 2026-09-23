using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using InventorySync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Repositories;

public class SyncRepository : ISyncRepository
{
    private readonly SyncDbContext _context;

    public SyncRepository(SyncDbContext context)
    {
        _context = context;
    }

    public Task AddRunAsync(SyncRun run, CancellationToken ct = default)
    {
        return _context.SyncRuns.AddAsync(run, ct).AsTask();
    }

    public async Task<PagedResult<SyncRun>> GetRunsAsync(
        SyncEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.SyncRuns.AsNoTracking();

        if (entityType.HasValue)
        {
            query = query.Where(x => x.EntityType == entityType.Value);
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

    public Task<SyncRun?> GetRunByIdAsync(int id, CancellationToken ct = default)
    {
        return _context.SyncRuns.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<SyncAuditEntry>> GetAuditEntriesAsync(
        int syncRunId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        return await _context.SyncAuditEntries
            .AsNoTracking()
            .Where(x => x.SyncRunId == syncRunId)
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public void AddAuditEntries(IEnumerable<SyncAuditEntry> entries)
    {
        _context.SyncAuditEntries.AddRange(entries);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
