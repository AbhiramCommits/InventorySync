using InventorySync.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySync.Infrastructure.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PoNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.PoNumber)
            .IsUnique();

        builder.Property(x => x.VendorCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.OrderDateUtc)
            .IsRequired();

        builder.Property(x => x.ExpectedDateUtc)
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ErpRecordId)
            .HasMaxLength(100);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_PurchaseOrders_OpenByStatus")
            .HasFilter("Status IN (1, 2)")
            .IncludeProperties(x => new { x.Id, x.PoNumber });
    }
}
