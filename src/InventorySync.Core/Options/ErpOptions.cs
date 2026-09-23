namespace InventorySync.Core.Options;

public class ErpOptions
{
    public const string SectionName = "Erp";

    public string BaseUrl { get; set; } = "http://localhost:8081";
}
