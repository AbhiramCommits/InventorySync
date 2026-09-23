using InventorySync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySync.Infrastructure.Data.Configurations;

public class SyncAuditEntryConfiguration : IEntityTypeConfiguration<SyncAuditEntry>
{
    public void Configure(EntityTypeBuilder<SyncAuditEntry> builder)
    {
        builder.ToTable("SyncAuditEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.EntityKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.FieldName)
            .HasMaxLength(100);

        builder.Property(x => x.OldValue)
            .HasMaxLength(500);

        builder.Property(x => x.NewValue)
            .HasMaxLength(500);

        builder.Property(x => x.Message)
            .HasMaxLength(1000);

        builder.Property(x => x.TimestampUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.SyncRunId, x.EntityType });

        builder.HasOne<SyncRun>()
            .WithMany()
            .HasForeignKey(x => x.SyncRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
