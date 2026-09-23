using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

public class SyncRunDto
{
    public int Id { get; set; }

    public int? ParentSyncRunId { get; set; }

    public SyncEntityType EntityType { get; set; }

    public DateTime StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public SyncRunStatus Status { get; set; }

    public int RecordsRead { get; set; }

    public int RecordsInserted { get; set; }

    public int RecordsUpdated { get; set; }

    public int RecordsFailed { get; set; }

    public string TriggeredBy { get; set; } = string.Empty;
}

public class SyncAuditEntryDto
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
