using InventorySync.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySync.Infrastructure.Data.Configurations;

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.QuantityOrdered)
            .IsRequired();

        builder.Property(x => x.QuantityReceived)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.HasIndex(x => x.PurchaseOrderId);

        builder.HasIndex(x => x.Sku)
            .HasDatabaseName("IX_PurchaseOrderLines_Sku_Open")
            .IncludeProperties(x => new { x.QuantityOrdered, x.QuantityReceived });
    }
}
