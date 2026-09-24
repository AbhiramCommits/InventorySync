using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

/// <summary>
/// Sync run data returned by the API.
/// </summary>
public class SyncRunDto
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

/// <summary>
/// Audit entry data returned by the API.
/// </summary>
public class SyncAuditEntryDto
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the sync run id.
    /// </summary>
    public int SyncRunId { get; set; }

    /// <summary>
    /// Gets or sets the entity type.
    /// </summary>
    public SyncEntityType EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity key.
    /// </summary>
    public string EntityKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    public SyncAuditAction Action { get; set; }

    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Gets or sets the old value.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the timestamp utc.
    /// </summary>
    public DateTime TimestampUtc { get; set; }
}
