namespace MockErp.Options;

public class MockErpOptions
{
    public const string SectionName = "Erp";

    public int InventoryCount { get; set; } = 12000;

    public int PurchaseOrderCount { get; set; } = 2500;

    public int Seed { get; set; } = 20240102;

    public int SkuOffset { get; set; } = 40000;

    public int PoNumberOffset { get; set; } = 3000;
}

public class FaultOptions
{
    public const string SectionName = "Faults";

    public double DefaultFailRate { get; set; }

    public double DefaultMalformedRate { get; set; }
}
