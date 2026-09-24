using System.Globalization;

using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Mapping;

namespace InventorySync.Infrastructure.Erp;

/// <summary>
/// Maps ERP wire records to domain entities with field-level validation instead of throwing.
/// </summary>
public static class ErpRecordMapper
{
    /// <summary>
    /// The erp date format.
    /// </summary>
    public const string ErpDateFormat = "yyyyMMddHHmmss";

    private static readonly IReadOnlyDictionary<string, PurchaseOrderStatus> StatusCodes =
        new Dictionary<string, PurchaseOrderStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["DRF"] = PurchaseOrderStatus.Draft,
            ["SUB"] = PurchaseOrderStatus.Submitted,
            ["PAR"] = PurchaseOrderStatus.PartiallyReceived,
            ["REC"] = PurchaseOrderStatus.Received,
            ["CAN"] = PurchaseOrderStatus.Cancelled,
        };

    /// <summary>
    /// Maps an ERP inventory record to an InventoryItem.
    /// </summary>
    public static ErpMappingResult<InventoryItem> MapInventoryItem(ErpInventoryItemRecord record)
    {
        var errors = new List<FieldError>();

        var sku = Trim(record.Sku);
        if (string.IsNullOrEmpty(sku))
        {
            errors.Add(new FieldError("ITM_SKU", "SKU is missing or blank."));
        }

        var name = Trim(record.Name);
        if (string.IsNullOrEmpty(name))
        {
            errors.Add(new FieldError("ITM_NAME", "Name is missing or blank."));
        }

        var quantity = ParseInt(record.QuantityOnHand, "ITM_QTY_ON_HAND", errors);
        var unitCost = ParseDecimal(record.UnitCost, "ITM_UNIT_COST", errors);
        var warehouseCode = Trim(record.WarehouseCode);
        if (string.IsNullOrEmpty(warehouseCode))
        {
            errors.Add(new FieldError("ITM_WHSE", "Warehouse code is missing or blank."));
        }

        var modifiedDtm = ParseDate(record.ModifiedDtm, "ITM_MOD_DTM", errors);

        if (errors.Count > 0 || sku is null || name is null || warehouseCode is null || quantity is null || unitCost is null || modifiedDtm is null)
        {
            return ErpMappingResult<InventoryItem>.Failure(errors);
        }

        var item = new InventoryItem
        {
            Sku = sku,
            Name = name,
            Description = Trim(record.Description),
            QuantityOnHand = quantity.Value,
            UnitCost = unitCost.Value,
            WarehouseCode = warehouseCode,
            ErpRecordId = Trim(record.ErpRecordId),
            LastSyncedUtc = modifiedDtm.Value,
        };

        return ErpMappingResult<InventoryItem>.Success(item);
    }

    /// <summary>
    /// Maps an ERP purchase order record to a PurchaseOrder.
    /// </summary>
    public static ErpMappingResult<PurchaseOrder> MapPurchaseOrder(ErpPurchaseOrderRecord record)
    {
        var errors = new List<FieldError>();

        var poNumber = Trim(record.PoNumber);
        if (string.IsNullOrEmpty(poNumber))
        {
            errors.Add(new FieldError("PO_NUMBER", "PO number is missing or blank."));
        }

        var vendorCode = Trim(record.VendorCode);
        if (string.IsNullOrEmpty(vendorCode))
        {
            errors.Add(new FieldError("PO_VENDOR", "Vendor code is missing or blank."));
        }

        PurchaseOrderStatus? status = null;
        var rawStatus = Trim(record.Status);
        if (string.IsNullOrEmpty(rawStatus))
        {
            errors.Add(new FieldError("PO_STATUS", "Status is missing or blank."));
        }
        else if (StatusCodes.TryGetValue(rawStatus, out var parsedStatus))
        {
            status = parsedStatus;
        }
        else
        {
            errors.Add(new FieldError("PO_STATUS", $"'{rawStatus}' is not a recognised status code (DRF, SUB, PAR, REC, CAN)."));
        }

        var orderDateUtc = ParseDate(record.OrderDateDtm, "PO_ORDER_DTM", errors);
        var expectedDateUtc = ParseDate(record.ExpectedDateDtm, "PO_EXPECTED_DTM", errors);
        var modifiedDtm = ParseDate(record.ModifiedDtm, "PO_MOD_DTM", errors);
        var totalAmount = ParseDecimal(record.TotalAmount, "PO_TOTAL_AMT", errors);

        var lines = new List<PurchaseOrderLine>();
        for (var i = 0; i < record.Lines.Count; i++)
        {
            var line = record.Lines[i];
            var lineErrors = new List<FieldError>();

            var lineSku = Trim(line.Sku);
            if (string.IsNullOrEmpty(lineSku))
            {
                lineErrors.Add(new FieldError($"PO_LINES[{i}].POL_SKU", "Line SKU is missing or blank."));
            }

            var quantityOrdered = ParseInt(line.QuantityOrdered, $"PO_LINES[{i}].POL_QTY_ORDERED", lineErrors);
            var quantityReceived = ParseOptionalInt(line.QuantityReceived, $"PO_LINES[{i}].POL_QTY_RECEIVED", lineErrors);
            var unitPrice = ParseDecimal(line.UnitPrice, $"PO_LINES[{i}].POL_UNIT_PRICE", lineErrors);

            if (lineErrors.Count > 0)
            {
                errors.AddRange(lineErrors);
                continue;
            }

            lines.Add(new PurchaseOrderLine
            {
                Sku = lineSku!,
                QuantityOrdered = quantityOrdered!.Value,
                QuantityReceived = quantityReceived ?? 0,
                UnitPrice = unitPrice!.Value,
            });
        }

        if (errors.Count > 0 || poNumber is null || vendorCode is null || status is null || orderDateUtc is null || expectedDateUtc is null || modifiedDtm is null || totalAmount is null)
        {
            return ErpMappingResult<PurchaseOrder>.Failure(errors);
        }

        var order = new PurchaseOrder
        {
            PoNumber = poNumber,
            VendorCode = vendorCode,
            Status = status.Value,
            OrderDateUtc = orderDateUtc.Value,
            ExpectedDateUtc = expectedDateUtc.Value,
            TotalAmount = totalAmount.Value,
            ErpRecordId = Trim(record.ErpRecordId),
            LastSyncedUtc = modifiedDtm.Value,
            Lines = lines,
        };

        return ErpMappingResult<PurchaseOrder>.Success(order);
    }

    private static string? Trim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ParseInt(string? value, string fieldName, List<FieldError> errors)
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(fieldName, "Value is missing."));
            return null;
        }

        if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        errors.Add(new FieldError(fieldName, $"'{trimmed}' is not a valid integer."));
        return null;
    }

    private static int? ParseOptionalInt(string? value, string fieldName, List<FieldError> errors)
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            return 0;
        }

        if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        errors.Add(new FieldError(fieldName, $"'{trimmed}' is not a valid integer."));
        return null;
    }

    private static decimal? ParseDecimal(string? value, string fieldName, List<FieldError> errors)
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(fieldName, "Value is missing."));
            return null;
        }

        if (decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        errors.Add(new FieldError(fieldName, $"'{trimmed}' is not a valid decimal number."));
        return null;
    }

    private static DateTime? ParseDate(string? value, string fieldName, List<FieldError> errors)
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(fieldName, "Value is missing."));
            return null;
        }

        if (DateTime.TryParseExact(trimmed, ErpDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }

        errors.Add(new FieldError(fieldName, $"'{trimmed}' is not a valid date in {ErpDateFormat} format."));
        return null;
    }
}
