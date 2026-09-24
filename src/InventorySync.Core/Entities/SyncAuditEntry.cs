using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

/// <summary>
/// Represents a single field-level change (or error) recorded during a sync run.
/// </summary>
public class SyncAuditEntry
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
