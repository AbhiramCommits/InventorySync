using System.Globalization;

namespace MockErp.Erp;

public static class ErpDataSource
{
    private static readonly DateTime EpochUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<ErpItemRecord> GenerateItems(int count, int seed, int skuOffset = 0)
    {
        var random = new Random(seed);
        var items = new List<ErpItemRecord>(count);

        for (var i = 1; i <= count; i++)
        {
            var sku = $"SKU-{skuOffset + i:D6}";
            var warehouseCode = $"WH{random.Next(1, 9):D2}";
            var description = random.Next(100) < 80 ? $"Description for {sku}" : null;
            var quantityOnHand = random.Next(0, 5001).ToString(CultureInfo.InvariantCulture);
            var unitCost = Math.Round((decimal)(random.NextDouble() * 499.99 + 0.01), 4).ToString(CultureInfo.InvariantCulture);
            var modifiedUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));

            items.Add(new ErpItemRecord(
                sku,
                $"Item {sku}",
                description,
                quantityOnHand,
                unitCost,
                warehouseCode,
                ErpDateFormats.Format(modifiedUtc),
                $"ERP-INV-{skuOffset + i:D6}"));
        }

        return items;
    }

    public static IReadOnlyList<ErpOrderRecord> GenerateOrders(int count, int seed, int itemCount, int skuOffset = 0, int poNumberOffset = 0)
    {
        var random = new Random(seed ^ 0x5F3759DF);
        var orders = new List<ErpOrderRecord>(count);

        for (var i = 1; i <= count; i++)
        {
            var poNumber = $"PO-{poNumberOffset + i:D6}";
            var vendorCode = $"VND-{random.Next(1, 41):D3}";

            var statusRoll = random.Next(100);
            var status = statusRoll switch
            {
                < 60 => "DRF",
                < 80 => "SUB",
                < 90 => "PAR",
                < 98 => "REC",
                _ => "CAN",
            };

            var lineCount = random.Next(1, 11);
            var lines = new List<ErpOrderLineRecord>(lineCount);
            var usedSkus = new HashSet<string>(StringComparer.Ordinal);
            var partialCandidates = new List<int>();

            for (var j = 0; j < lineCount; j++)
            {
                string sku;
                do
                {
                    sku = $"SKU-{skuOffset + random.Next(1, itemCount + 1):D6}";
                }
                while (!usedSkus.Add(sku));

                var quantityOrdered = random.Next(1, 1001);
                var quantityReceived = status switch
                {
                    "REC" => quantityOrdered,
                    "PAR" => random.Next(0, quantityOrdered),
                    _ => 0,
                };

                if (status == "PAR" && quantityOrdered >= 2 && quantityReceived == 0)
                {
                    partialCandidates.Add(j);
                }

                var unitPrice = Math.Round((decimal)(random.NextDouble() * 99.99 + 0.01), 4).ToString(CultureInfo.InvariantCulture);

                lines.Add(new ErpOrderLineRecord(
                    sku,
                    quantityOrdered.ToString(CultureInfo.InvariantCulture),
                    quantityReceived.ToString(CultureInfo.InvariantCulture),
                    unitPrice));
            }

            if (status == "PAR" && partialCandidates.Count > 0)
            {
                var pick = partialCandidates[random.Next(partialCandidates.Count)];
                var existing = lines[pick];
                var received = random.Next(1, int.Parse(existing.QuantityOrdered, CultureInfo.InvariantCulture));
                lines[pick] = existing with { QuantityReceived = received.ToString(CultureInfo.InvariantCulture) };
            }

            var totalAmount = Math.Round(
                lines.Sum(l => int.Parse(l.QuantityOrdered, CultureInfo.InvariantCulture) * decimal.Parse(l.UnitPrice, CultureInfo.InvariantCulture)),
                2).ToString(CultureInfo.InvariantCulture);

            var orderDateUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));
            var expectedDateUtc = orderDateUtc.AddDays(random.Next(7, 31));
            var modifiedUtc = EpochUtc.AddDays(random.Next(0, 365)).AddMinutes(random.Next(0, 1440));

            orders.Add(new ErpOrderRecord(
                poNumber,
                vendorCode,
                status,
                ErpDateFormats.Format(orderDateUtc),
                ErpDateFormats.Format(expectedDateUtc),
                totalAmount,
                ErpDateFormats.Format(modifiedUtc),
                $"ERP-PO-{poNumberOffset + i:D6}",
                lines));
        }

        return orders;
    }
}
