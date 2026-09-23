using System.Globalization;

using InventorySync.Core.Dtos;
using InventorySync.Core.Dtos.Erp;

namespace InventorySync.Tests.TestHelpers;

internal static class ErpRecordFactory
{
    public static ErpInventoryItemRecord Item(
        string sku,
        string? quantityOnHand = "10",
        string? unitCost = "12.5000",
        string? warehouseCode = "WH01",
        string? modifiedDtm = "20240101120000",
        string? name = null,
        string? description = null,
        string? erpRecordId = null)
    {
        return new ErpInventoryItemRecord
        {
            Sku = sku,
            Name = name ?? $"Item {sku}",
            Description = description,
            QuantityOnHand = quantityOnHand,
            UnitCost = unitCost,
            WarehouseCode = warehouseCode,
            ModifiedDtm = modifiedDtm,
            ErpRecordId = erpRecordId,
        };
    }

    public static ErpPurchaseOrderRecord Order(
        string poNumber,
        string vendorCode = "VND-001",
        string status = "DRF",
        string? orderDateDtm = "20240101100000",
        string? expectedDateDtm = "20240115100000",
        string? totalAmount = null,
        string? modifiedDtm = "20240101120000",
        IReadOnlyList<ErpPurchaseOrderLineRecord>? lines = null)
    {
        lines ??= new List<ErpPurchaseOrderLineRecord>
        {
            Line("SKU-A", "10", "0", "1.5000"),
        };

        var computedTotal = lines.Sum(l =>
        {
            _ = int.TryParse(l.QuantityOrdered ?? string.Empty, out var qty);
            _ = decimal.TryParse(l.UnitPrice ?? string.Empty, out var price);
            return qty * price;
        });

        return new ErpPurchaseOrderRecord
        {
            PoNumber = poNumber,
            VendorCode = vendorCode,
            Status = status,
            OrderDateDtm = orderDateDtm,
            ExpectedDateDtm = expectedDateDtm,
            TotalAmount = totalAmount ?? computedTotal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ModifiedDtm = modifiedDtm,
            ErpRecordId = $"ERP-PO-{poNumber}",
            Lines = lines.ToList(),
        };
    }

    public static ErpPurchaseOrderLineRecord Line(
        string sku,
        string quantityOrdered,
        string quantityReceived,
        string unitPrice)
    {
        return new ErpPurchaseOrderLineRecord
        {
            Sku = sku,
            QuantityOrdered = quantityOrdered,
            QuantityReceived = quantityReceived,
            UnitPrice = unitPrice,
        };
    }

    public static CreateInventoryItemRequest CreateItemRequest(string sku = "SKU-LOCAL-1")
    {
        return new CreateInventoryItemRequest
        {
            Sku = sku,
            Name = "Local Item",
            Description = null,
            QuantityOnHand = 7,
            UnitCost = 3.5m,
            WarehouseCode = "WH01",
        };
    }
}
