using InventorySync.Core.Enums;
using InventorySync.Infrastructure.Erp;

namespace InventorySync.Tests;

public class ErpDataGeneratorTests
{
    [Fact]
    public void GenerateInventory_IsDeterministic()
    {
        var first = ErpDataGenerator.GenerateInventory(1000, ErpDataGenerator.DefaultSeed).ToList();
        var second = ErpDataGenerator.GenerateInventory(1000, ErpDataGenerator.DefaultSeed).ToList();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
        }
    }

    [Fact]
    public void GenerateInventory_ProducesValidRecords()
    {
        var records = ErpDataGenerator.GenerateInventory(500, ErpDataGenerator.DefaultSeed).ToList();

        Assert.Equal(500, records.Count);
        Assert.Equal(500, records.Select(r => r.Sku).Distinct().Count());
        Assert.All(records, r =>
        {
            Assert.StartsWith("SKU-", r.Sku);
            Assert.StartsWith("ERP-INV-", r.ErpRecordId);
            Assert.Contains(r.WarehouseCode, new[] { "WH01", "WH02", "WH03", "WH04", "WH05", "WH06", "WH07", "WH08" });
            Assert.InRange(r.QuantityOnHand, 0, 5000);
            Assert.InRange(r.UnitCost, 0.01m, 500m);
            Assert.Equal(4, DecimalPlaces(r.UnitCost));
        });
    }

    [Fact]
    public void GeneratePurchaseOrders_IsDeterministic()
    {
        var first = ErpDataGenerator.GeneratePurchaseOrders(200, ErpDataGenerator.DefaultSeed, 5000).ToList();
        var second = ErpDataGenerator.GeneratePurchaseOrders(200, ErpDataGenerator.DefaultSeed, 5000).ToList();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].ErpRecordId, second[i].ErpRecordId);
            Assert.Equal(first[i].PoNumber, second[i].PoNumber);
            Assert.Equal(first[i].VendorCode, second[i].VendorCode);
            Assert.Equal(first[i].Status, second[i].Status);
            Assert.Equal(first[i].OrderDateUtc, second[i].OrderDateUtc);
            Assert.Equal(first[i].ExpectedDateUtc, second[i].ExpectedDateUtc);
            Assert.Equal(first[i].TotalAmount, second[i].TotalAmount);
            Assert.Equal(first[i].ModifiedUtc, second[i].ModifiedUtc);
            Assert.Equal(first[i].Lines.Count, second[i].Lines.Count);
            for (var j = 0; j < first[i].Lines.Count; j++)
            {
                Assert.Equal(first[i].Lines[j], second[i].Lines[j]);
            }
        }
    }

    [Fact]
    public void GeneratePurchaseOrders_ProducesValidRecords()
    {
        var records = ErpDataGenerator.GeneratePurchaseOrders(200, ErpDataGenerator.DefaultSeed, 5000).ToList();

        Assert.Equal(200, records.Count);
        Assert.Equal(200, records.Select(r => r.PoNumber).Distinct().Count());
        Assert.All(records, r =>
        {
            Assert.StartsWith("PO-", r.PoNumber);
            Assert.StartsWith("ERP-PO-", r.ErpRecordId);
            Assert.StartsWith("VND-", r.VendorCode);
            Assert.InRange(r.Lines.Count, 1, 10);
            Assert.Equal(r.Lines.Count, r.Lines.Select(l => l.Sku).Distinct().Count());
            Assert.Equal(r.TotalAmount, Math.Round(r.Lines.Sum(l => l.QuantityOrdered * l.UnitPrice), 2));
            Assert.Equal(2, DecimalPlaces(r.TotalAmount));

            if (r.Status == PurchaseOrderStatus.Received)
            {
                Assert.All(r.Lines, l => Assert.Equal(l.QuantityOrdered, l.QuantityReceived));
            }

            if (r.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Cancelled)
            {
                Assert.All(r.Lines, l => Assert.Equal(0, l.QuantityReceived));
            }
        });
    }

    private static int DecimalPlaces(decimal value)
    {
        return (decimal.GetBits(value)[3] >> 16) & 0xFF;
    }
}
