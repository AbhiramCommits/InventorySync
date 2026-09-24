using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

/// <summary>
/// Represents one execution of a synchronisation against the ERP system.
/// </summary>
public class SyncRun
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the parent sync run id.
    /// </summary>
    public int? ParentSyncRunId { get; set; }

    /// <summary>
    /// Gets or sets the parent sync run.
    /// </summary>
    public SyncRun? ParentSyncRun { get; set; }

    /// <summary>
    /// Gets or sets the entity type.
    /// </summary>
    public SyncEntityType EntityType { get; set; }

    /// <summary>
    /// Gets or sets the started utc.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the completed utc.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public SyncRunStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the records read.
    /// </summary>
    public int RecordsRead { get; set; }

    /// <summary>
    /// Gets or sets the records inserted.
    /// </summary>
    public int RecordsInserted { get; set; }

    /// <summary>
    /// Gets or sets the records updated.
    /// </summary>
    public int RecordsUpdated { get; set; }

    /// <summary>
    /// Gets or sets the records failed.
    /// </summary>
    public int RecordsFailed { get; set; }

    /// <summary>
    /// Gets or sets the triggered by.
    /// </summary>
    public string TriggeredBy { get; set; } = string.Empty;
}
