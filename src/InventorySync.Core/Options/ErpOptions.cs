namespace InventorySync.Core.Options;

/// <summary>
/// Configuration for the ERP HTTP integration.
/// </summary>
public class ErpOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    public const string SectionName = "Erp";

    /// <summary>
    /// Gets or sets the base url.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8081";
}
