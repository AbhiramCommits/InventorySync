namespace InventorySync.Core.Options;

/// <summary>
/// Configuration for the CORS policy.
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// The policy name.
    /// </summary>
    public const string PolicyName = "Default";

    /// <summary>
    /// Gets or sets the allowed origins.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
