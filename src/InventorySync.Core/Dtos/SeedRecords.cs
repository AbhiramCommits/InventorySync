using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

public sealed record SeedInventoryRecord(
    string ErpRecordId,
    string Sku,
    string Name,
    string? Description,
    int QuantityOnHand,
    decimal UnitCost,
    string WarehouseCode,
    DateTime ModifiedUtc);

public sealed record SeedPurchaseOrderRecord(
    string ErpRecordId,
    string PoNumber,
    string VendorCode,
    PurchaseOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime ExpectedDateUtc,
    decimal TotalAmount,
    DateTime ModifiedUtc,
    IReadOnlyList<SeedPurchaseOrderLineRecord> Lines);

public sealed record SeedPurchaseOrderLineRecord(
    string Sku,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitPrice);
