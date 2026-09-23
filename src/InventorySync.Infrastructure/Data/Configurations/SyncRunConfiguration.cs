using InventorySync.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySync.Infrastructure.Data.Configurations;

public class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRuns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.StartedUtc)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.TriggeredBy)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(x => x.ParentSyncRun)
            .WithMany()
            .HasForeignKey(x => x.ParentSyncRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EntityType, x.StartedUtc });
    }
}
