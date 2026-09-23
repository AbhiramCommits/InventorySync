using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

public class SyncAuditEntry
{
    public int Id { get; set; }

    public int SyncRunId { get; set; }

    public SyncEntityType EntityType { get; set; }

    public string EntityKey { get; set; } = string.Empty;

    public SyncAuditAction Action { get; set; }

    public string? FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Message { get; set; }

    public DateTime TimestampUtc { get; set; }
}
