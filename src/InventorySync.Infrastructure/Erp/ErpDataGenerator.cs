using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;

namespace InventorySync.Infrastructure.Erp;

/// <summary>
/// Deterministic pseudo-random generator used by the data seeder and the mock ERP.
/// </summary>
public static class ErpDataGenerator
{
    /// <summary>
    /// The default seed.
    /// </summary>
    public const int DefaultSeed = 20240101;

    private static readonly DateTime EpochUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Generates deterministic inventory records.
    /// </summary>
    public static IEnumerable<SeedInventoryRecord> GenerateInventory(int count, int seed)
    {
        var random = new Random(seed);

        for (var i = 1; i <= count; i++)
        {
            var sku = $"SKU-{i:D6}";
            var warehouseCode = $"WH{random.Next(1, 9):D2}";
            var description = random.Next(100) < 80 ? $"Description for {sku}" : null;
            var quantityOnHand = random.Next(0, 5001);
            var unitCost = Math.Round((decimal)(random.NextDouble() * 499.99 + 0.01), 4);
            var modifiedUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));

            yield return new SeedInventoryRecord(
                $"ERP-INV-{i:D6}",
                sku,
                $"Item {sku}",
                description,
                quantityOnHand,
                unitCost,
                warehouseCode,
                modifiedUtc);
        }
    }

    /// <summary>
    /// Generates deterministic purchase order records.
    /// </summary>
    public static IEnumerable<SeedPurchaseOrderRecord> GeneratePurchaseOrders(int count, int seed, int itemCount)
    {
        var random = new Random(seed ^ 0x5F3759DF);

        for (var i = 1; i <= count; i++)
        {
            var poNumber = $"PO-{i:D6}";
            var vendorCode = $"VND-{random.Next(1, 41):D3}";

            var statusRoll = random.Next(100);
            var status = statusRoll switch
            {
                < 60 => PurchaseOrderStatus.Draft,
                < 80 => PurchaseOrderStatus.Submitted,
                < 90 => PurchaseOrderStatus.PartiallyReceived,
                < 98 => PurchaseOrderStatus.Received,
                _ => PurchaseOrderStatus.Cancelled,
            };

            var lineCount = random.Next(1, 11);
            var lines = new List<SeedPurchaseOrderLineRecord>(lineCount);
            var usedSkus = new HashSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < lineCount; j++)
            {
                string sku;
                do
                {
                    sku = $"SKU-{random.Next(1, itemCount + 1):D6}";
                }
                while (!usedSkus.Add(sku));

                var quantityOrdered = random.Next(1, 1001);
                var quantityReceived = status switch
                {
                    PurchaseOrderStatus.Received => quantityOrdered,
                    PurchaseOrderStatus.PartiallyReceived => random.Next(0, quantityOrdered),
                    _ => 0,
                };
                var unitPrice = Math.Round((decimal)(random.NextDouble() * 99.99 + 0.01), 4);

                lines.Add(new SeedPurchaseOrderLineRecord(sku, quantityOrdered, quantityReceived, unitPrice));
            }

            if (status == PurchaseOrderStatus.PartiallyReceived)
            {
                var partialCandidates = lines
                    .Select((line, index) => (Line: line, Index: index))
                    .Where(x => x.Line.QuantityOrdered >= 2)
                    .ToList();

                if (partialCandidates.Count > 0)
                {
                    var pick = partialCandidates[random.Next(partialCandidates.Count)];
                    var received = random.Next(1, pick.Line.QuantityOrdered);
                    lines[pick.Index] = pick.Line with { QuantityReceived = received };
                }
            }

            var totalAmount = Math.Round(lines.Sum(l => l.QuantityOrdered * l.UnitPrice), 2);
            var orderDateUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));
            var expectedDateUtc = orderDateUtc.AddDays(random.Next(7, 31));
            var modifiedUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));

            yield return new SeedPurchaseOrderRecord(
                $"ERP-PO-{i:D6}",
                poNumber,
                vendorCode,
                status,
                orderDateUtc,
                expectedDateUtc,
                totalAmount,
                modifiedUtc,
                lines);
        }
    }
}
