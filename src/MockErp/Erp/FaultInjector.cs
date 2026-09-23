namespace MockErp.Erp;

public static class FaultInjector
{
    public static bool ShouldFailPage(int page, double failRate)
    {
        if (failRate <= 0)
        {
            return false;
        }

        return new Random(page * 397).NextDouble() < failRate;
    }

    public static ErpItemRecord CorruptItem(ErpItemRecord item, double malformedRate, int page, int index)
    {
        if (malformedRate <= 0)
        {
            return item;
        }

        var random = new Random((page * 397) ^ ((index + 1) * 31));
        if (random.NextDouble() >= malformedRate)
        {
            return item;
        }

        return random.Next(2) == 0
            ? item with { Sku = string.Empty }
            : item with { QuantityOnHand = "N/A" };
    }

    public static ErpOrderRecord CorruptOrder(ErpOrderRecord order, double malformedRate, int page, int index)
    {
        if (malformedRate <= 0)
        {
            return order;
        }

        var random = new Random((page * 397) ^ ((index + 1) * 31));
        if (random.NextDouble() >= malformedRate)
        {
            return order;
        }

        return random.Next(2) == 0
            ? order with { PoNumber = string.Empty }
            : order with { TotalAmount = "NOT-A-NUMBER" };
    }
}
