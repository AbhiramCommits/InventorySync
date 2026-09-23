using InventorySync.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Data;

public class SyncDbContext : DbContext
{
    public SyncDbContext(DbContextOptions<SyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    public DbSet<SyncAuditEntry> SyncAuditEntries => Set<SyncAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SyncDbContext).Assembly);
    }
}
