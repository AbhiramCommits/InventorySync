namespace InventorySync.Core.Enums;

/// <summary>
/// How conflicts between local changes and ERP changes are resolved.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Represents the erp wins option.
    /// </summary>
    ErpWins = 0,
    /// <summary>
    /// Represents the local wins option.
    /// </summary>
    LocalWins = 1,
    /// <summary>
    /// Represents the newer wins option.
    /// </summary>
    NewerWins = 2,
}
