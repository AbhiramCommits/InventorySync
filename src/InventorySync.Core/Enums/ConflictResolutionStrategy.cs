namespace InventorySync.Core.Enums;

public enum ConflictResolutionStrategy
{
    ErpWins = 0,
    LocalWins = 1,
    NewerWins = 2,
}
