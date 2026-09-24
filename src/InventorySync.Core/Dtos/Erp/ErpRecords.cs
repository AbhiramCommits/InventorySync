using System.Text.Json.Serialization;

namespace InventorySync.Core.Dtos.Erp;

/// <summary>
/// Wire shape of an inventory item as returned by the mock ERP REST API.
/// </summary>
public sealed class ErpInventoryItemRecord
{
    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    [JsonPropertyName("ITM_SKU")]
    public string? Sku { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    [JsonPropertyName("ITM_NAME")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [JsonPropertyName("ITM_DESC")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    [JsonPropertyName("ITM_QTY_ON_HAND")]
    public string? QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    [JsonPropertyName("ITM_UNIT_COST")]
    public string? UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    [JsonPropertyName("ITM_WHSE")]
    public string? WarehouseCode { get; set; }

    /// <summary>
    /// Gets or sets the modified dtm.
    /// </summary>
    [JsonPropertyName("ITM_MOD_DTM")]
    public string? ModifiedDtm { get; set; }

    /// <summary>
    /// Gets or sets the erp record id.
    /// </summary>
    [JsonPropertyName("ITM_ERP_ID")]
    public string? ErpRecordId { get; set; }
}

/// <summary>
/// Wire shape of a purchase order as returned by the mock ERP REST API.
/// </summary>
public sealed class ErpPurchaseOrderRecord
{
    /// <summary>
    /// Gets or sets the po number.
    /// </summary>
    [JsonPropertyName("PO_NUMBER")]
    public string? PoNumber { get; set; }

    /// <summary>
    /// Gets or sets the vendor code.
    /// </summary>
    [JsonPropertyName("PO_VENDOR")]
    public string? VendorCode { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [JsonPropertyName("PO_STATUS")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the order date dtm.
    /// </summary>
    [JsonPropertyName("PO_ORDER_DTM")]
    public string? OrderDateDtm { get; set; }

    /// <summary>
    /// Gets or sets the expected date dtm.
    /// </summary>
    [JsonPropertyName("PO_EXPECTED_DTM")]
    public string? ExpectedDateDtm { get; set; }

    /// <summary>
    /// Gets or sets the total amount.
    /// </summary>
    [JsonPropertyName("PO_TOTAL_AMT")]
    public string? TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the modified dtm.
    /// </summary>
    [JsonPropertyName("PO_MOD_DTM")]
    public string? ModifiedDtm { get; set; }

    /// <summary>
    /// Gets or sets the erp record id.
    /// </summary>
    [JsonPropertyName("PO_ERP_ID")]
    public string? ErpRecordId { get; set; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    [JsonPropertyName("PO_LINES")]
    public List<ErpPurchaseOrderLineRecord> Lines { get; set; } = new();
}

/// <summary>
/// Wire shape of a purchase order line as returned by the mock ERP REST API.
/// </summary>
public sealed class ErpPurchaseOrderLineRecord
{
    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    [JsonPropertyName("POL_SKU")]
    public string? Sku { get; set; }

    /// <summary>
    /// Gets or sets the quantity ordered.
    /// </summary>
    [JsonPropertyName("POL_QTY_ORDERED")]
    public string? QuantityOrdered { get; set; }

    /// <summary>
    /// Gets or sets the quantity received.
    /// </summary>
    [JsonPropertyName("POL_QTY_RECEIVED")]
    public string? QuantityReceived { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    [JsonPropertyName("POL_UNIT_PRICE")]
    public string? UnitPrice { get; set; }
}

/// <summary>
/// Wire shape of an item detail returned by the ERP SOAP endpoint.
/// </summary>
public sealed class ErpItemDetailRecord
{
    /// <summary>
    /// Gets or sets the sku.
    /// </summary>
    [JsonPropertyName("ITM_SKU")]
    public string? Sku { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    [JsonPropertyName("ITM_NAME")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [JsonPropertyName("ITM_DESC")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    [JsonPropertyName("ITM_QTY_ON_HAND")]
    public string? QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    [JsonPropertyName("ITM_UNIT_COST")]
    public string? UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the warehouse code.
    /// </summary>
    [JsonPropertyName("ITM_WHSE")]
    public string? WarehouseCode { get; set; }

    /// <summary>
    /// Gets or sets the modified dtm.
    /// </summary>
    [JsonPropertyName("ITM_MOD_DTM")]
    public string? ModifiedDtm { get; set; }
}
