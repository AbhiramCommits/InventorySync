using System.Text.Json.Serialization;

namespace InventorySync.Core.Dtos.Erp;

public sealed class ErpInventoryItemRecord
{
    [JsonPropertyName("ITM_SKU")]
    public string? Sku { get; set; }

    [JsonPropertyName("ITM_NAME")]
    public string? Name { get; set; }

    [JsonPropertyName("ITM_DESC")]
    public string? Description { get; set; }

    [JsonPropertyName("ITM_QTY_ON_HAND")]
    public string? QuantityOnHand { get; set; }

    [JsonPropertyName("ITM_UNIT_COST")]
    public string? UnitCost { get; set; }

    [JsonPropertyName("ITM_WHSE")]
    public string? WarehouseCode { get; set; }

    [JsonPropertyName("ITM_MOD_DTM")]
    public string? ModifiedDtm { get; set; }

    [JsonPropertyName("ITM_ERP_ID")]
    public string? ErpRecordId { get; set; }
}

public sealed class ErpPurchaseOrderRecord
{
    [JsonPropertyName("PO_NUMBER")]
    public string? PoNumber { get; set; }

    [JsonPropertyName("PO_VENDOR")]
    public string? VendorCode { get; set; }

    [JsonPropertyName("PO_STATUS")]
    public string? Status { get; set; }

    [JsonPropertyName("PO_ORDER_DTM")]
    public string? OrderDateDtm { get; set; }

    [JsonPropertyName("PO_EXPECTED_DTM")]
    public string? ExpectedDateDtm { get; set; }

    [JsonPropertyName("PO_TOTAL_AMT")]
    public string? TotalAmount { get; set; }

    [JsonPropertyName("PO_MOD_DTM")]
    public string? ModifiedDtm { get; set; }

    [JsonPropertyName("PO_ERP_ID")]
    public string? ErpRecordId { get; set; }

    [JsonPropertyName("PO_LINES")]
    public List<ErpPurchaseOrderLineRecord> Lines { get; set; } = new();
}

public sealed class ErpPurchaseOrderLineRecord
{
    [JsonPropertyName("POL_SKU")]
    public string? Sku { get; set; }

    [JsonPropertyName("POL_QTY_ORDERED")]
    public string? QuantityOrdered { get; set; }

    [JsonPropertyName("POL_QTY_RECEIVED")]
    public string? QuantityReceived { get; set; }

    [JsonPropertyName("POL_UNIT_PRICE")]
    public string? UnitPrice { get; set; }
}

public sealed class ErpItemDetailRecord
{
    [JsonPropertyName("ITM_SKU")]
    public string? Sku { get; set; }

    [JsonPropertyName("ITM_NAME")]
    public string? Name { get; set; }

    [JsonPropertyName("ITM_DESC")]
    public string? Description { get; set; }

    [JsonPropertyName("ITM_QTY_ON_HAND")]
    public string? QuantityOnHand { get; set; }

    [JsonPropertyName("ITM_UNIT_COST")]
    public string? UnitCost { get; set; }

    [JsonPropertyName("ITM_WHSE")]
    public string? WarehouseCode { get; set; }

    [JsonPropertyName("ITM_MOD_DTM")]
    public string? ModifiedDtm { get; set; }
}
