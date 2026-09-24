namespace InventorySync.Core.Enums;

/// <summary>
/// The action recorded for a single entity or field during a sync run.
/// </summary>
public enum SyncAuditAction
{
    /// <summary>
    /// Represents the insert option.
    /// </summary>
    Insert = 0,
    /// <summary>
    /// Represents the update option.
    /// </summary>
    Update = 1,
    /// <summary>
    /// Represents the skip option.
    /// </summary>
    Skip = 2,
    /// <summary>
    /// Represents the conflict resolved option.
    /// </summary>
    ConflictResolved = 3,
    /// <summary>
    /// Represents the error option.
    /// </summary>
    Error = 4,
}
