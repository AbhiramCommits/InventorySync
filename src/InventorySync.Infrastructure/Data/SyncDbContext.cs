using InventorySync.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Data;

/// <summary>
/// EF Core database context for the InventorySync database.
/// </summary>
public class SyncDbContext : DbContext
{
    /// <summary>
    /// sync db context.
    /// </summary>
    public SyncDbContext(DbContextOptions<SyncDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Inventory items.
    /// </summary>
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    /// <summary>
    /// Purchase orders.
    /// </summary>
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    /// <summary>
    /// Purchase order lines.
    /// </summary>
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    /// <summary>
    /// Sync runs.
    /// </summary>
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    /// <summary>
    /// Sync audit entries.
    /// </summary>
    public DbSet<SyncAuditEntry> SyncAuditEntries => Set<SyncAuditEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SyncDbContext).Assembly);
    }
}
