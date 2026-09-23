using InventorySync.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySync.Infrastructure.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Sku)
            .IsUnique();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.QuantityOnHand)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.WarehouseCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ErpRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.WarehouseCode, x.LastSyncedUtc });

        builder.HasIndex(x => new { x.WarehouseCode, x.QuantityOnHand })
            .HasDatabaseName("IX_InventoryItems_LowStock")
            .HasFilter("QuantityOnHand < 25");

        builder.HasIndex(x => x.WarehouseCode)
            .HasDatabaseName("IX_InventoryItems_Whse_Valuation")
            .IncludeProperties(x => new { x.QuantityOnHand, x.UnitCost });
    }
}
