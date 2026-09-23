using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

public sealed record ErpInventoryRecord(
    string ErpRecordId,
    string Sku,
    string Name,
    string? Description,
    int QuantityOnHand,
    decimal UnitCost,
    string WarehouseCode,
    DateTime ModifiedUtc);

public sealed record ErpPurchaseOrderRecord(
    string ErpRecordId,
    string PoNumber,
    string VendorCode,
    PurchaseOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime ExpectedDateUtc,
    decimal TotalAmount,
    DateTime ModifiedUtc,
    IReadOnlyList<ErpPurchaseOrderLineRecord> Lines);

public sealed record ErpPurchaseOrderLineRecord(
    string Sku,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitPrice);
