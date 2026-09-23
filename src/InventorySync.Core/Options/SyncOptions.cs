using InventorySync.Core.Enums;

namespace InventorySync.Core.Options;

public class SyncOptions
{
    public const string SectionName = "Sync";

    public int PageSize { get; set; } = 500;

    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.NewerWins;
}
