using InventorySync.Core.Enums;

namespace InventorySync.Core.Entities;

public class SyncRun
{
    public int Id { get; set; }

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
