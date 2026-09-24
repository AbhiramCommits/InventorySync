namespace InventorySync.Core.Enums;

/// <summary>
/// The terminal state of a sync run.
/// </summary>
public enum SyncRunStatus
{
    /// <summary>
    /// Represents the running option.
    /// </summary>
    Running = 0,
    /// <summary>
    /// Represents the succeeded option.
    /// </summary>
    Succeeded = 1,
    /// <summary>
    /// Represents the failed option.
    /// </summary>
    Failed = 2,
    /// <summary>
    /// Represents the partial success option.
    /// </summary>
    PartialSuccess = 3,
}
