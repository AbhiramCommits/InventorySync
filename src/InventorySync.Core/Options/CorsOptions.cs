namespace InventorySync.Core.Options;

public class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "Default";

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
