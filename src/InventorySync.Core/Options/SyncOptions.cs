using InventorySync.Core.Enums;

namespace InventorySync.Core.Options;

/// <summary>
/// Configuration for the sync engine.
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    public const string SectionName = "Sync";

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 500;

    /// <summary>
    /// Gets or sets the conflict resolution.
    /// </summary>
    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.NewerWins;
}
