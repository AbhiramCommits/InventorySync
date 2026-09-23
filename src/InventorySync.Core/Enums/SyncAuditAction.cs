namespace InventorySync.Core.Enums;

public enum SyncAuditAction
{
    Insert = 0,
    Update = 1,
    Skip = 2,
    ConflictResolved = 3,
    Error = 4,
}
