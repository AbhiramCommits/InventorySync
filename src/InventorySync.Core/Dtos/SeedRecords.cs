using InventorySync.Core.Enums;

namespace InventorySync.Core.Dtos;

/// <summary>
/// Deterministic seed data shape for inventory items.
/// </summary>
public sealed record SeedInventoryRecord(
    string ErpRecordId,
    string Sku,
    string Name,
    string? Description,
    int QuantityOnHand,
    decimal UnitCost,
    string WarehouseCode,
    DateTime ModifiedUtc);

/// <summary>
/// Deterministic seed data shape for purchase orders.
/// </summary>
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

/// <summary>
/// Deterministic seed data shape for purchase order lines.
/// </summary>
public sealed record SeedPurchaseOrderLineRecord(
    string Sku,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitPrice);
