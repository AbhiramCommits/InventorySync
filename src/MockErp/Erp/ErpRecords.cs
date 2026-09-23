using System.Globalization;
using System.Text.Json.Serialization;

namespace MockErp.Erp;

public sealed record ErpItemRecord(
    [property: JsonPropertyName("ITM_SKU")] string Sku,
    [property: JsonPropertyName("ITM_NAME")] string Name,
    [property: JsonPropertyName("ITM_DESC")] string? Description,
    [property: JsonPropertyName("ITM_QTY_ON_HAND")] string QuantityOnHand,
    [property: JsonPropertyName("ITM_UNIT_COST")] string UnitCost,
    [property: JsonPropertyName("ITM_WHSE")] string WarehouseCode,
    [property: JsonPropertyName("ITM_MOD_DTM")] string ModifiedDtm,
    [property: JsonPropertyName("ITM_ERP_ID")] string ErpRecordId);

public sealed record ErpOrderRecord(
    [property: JsonPropertyName("PO_NUMBER")] string PoNumber,
    [property: JsonPropertyName("PO_VENDOR")] string VendorCode,
    [property: JsonPropertyName("PO_STATUS")] string Status,
    [property: JsonPropertyName("PO_ORDER_DTM")] string OrderDateDtm,
    [property: JsonPropertyName("PO_EXPECTED_DTM")] string ExpectedDateDtm,
    [property: JsonPropertyName("PO_TOTAL_AMT")] string TotalAmount,
    [property: JsonPropertyName("PO_MOD_DTM")] string ModifiedDtm,
    [property: JsonPropertyName("PO_ERP_ID")] string ErpRecordId,
    [property: JsonPropertyName("PO_LINES")] IReadOnlyList<ErpOrderLineRecord> Lines);

public sealed record ErpOrderLineRecord(
    [property: JsonPropertyName("POL_SKU")] string Sku,
    [property: JsonPropertyName("POL_QTY_ORDERED")] string QuantityOrdered,
    [property: JsonPropertyName("POL_QTY_RECEIVED")] string QuantityReceived,
    [property: JsonPropertyName("POL_UNIT_PRICE")] string UnitPrice);

public sealed record PageEnvelope<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class ErpDateFormats
{
    public const string Compact = "yyyyMMddHHmmss";

    public static string Format(DateTime value) => value.ToString(Compact, CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out DateTime result) =>
        DateTime.TryParseExact(value, Compact, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
