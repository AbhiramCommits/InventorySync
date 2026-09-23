using InventorySync.Core.Enums;
using InventorySync.Infrastructure.Erp;
using InventorySync.Tests.TestHelpers;

namespace InventorySync.Tests;

public class ErpRecordMapperTests
{
    [Fact]
    public void MapInventoryItem_ValidRecord_ParsesAllFields()
    {
        var result = ErpRecordMapper.MapInventoryItem(
            ErpRecordFactory.Item(
                sku: " SKU-000001 ",
                quantityOnHand: "123",
                unitCost: "45.6789",
                warehouseCode: " WH02 ",
                modifiedDtm: "20240315103000",
                name: " Widget ",
                description: " A widget ",
                erpRecordId: " ERP-1 "));

        Assert.True(result.IsSuccess);
        var item = result.Value!;
        Assert.Equal("SKU-000001", item.Sku);
        Assert.Equal("Widget", item.Name);
        Assert.Equal("A widget", item.Description);
        Assert.Equal(123, item.QuantityOnHand);
        Assert.Equal(45.6789m, item.UnitCost);
        Assert.Equal("WH02", item.WarehouseCode);
        Assert.Equal("ERP-1", item.ErpRecordId);
        Assert.Equal(new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc), item.LastSyncedUtc);
    }

    [Fact]
    public void MapInventoryItem_MissingSku_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item(sku: ""));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "ITM_SKU");
    }

    [Fact]
    public void MapInventoryItem_NonNumericQuantity_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "N/A"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "ITM_QTY_ON_HAND" && e.Message.Contains("N/A"));
    }

    [Fact]
    public void MapInventoryItem_InvalidDate_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", modifiedDtm: "yesterday"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "ITM_MOD_DTM");
    }

    [Fact]
    public void MapInventoryItem_NullOptionalFields_AreAccepted()
    {
        var result = ErpRecordMapper.MapInventoryItem(
            ErpRecordFactory.Item("SKU-000001", description: null, erpRecordId: null));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Description);
        Assert.Null(result.Value.ErpRecordId);
    }

    [Fact]
    public void MapPurchaseOrder_ValidRecord_MapsStatusDatesAndLines()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order(
            " PO-000001 ",
            vendorCode: " VND-007 ",
            status: "SUB",
            orderDateDtm: "20240201080000",
            expectedDateDtm: "20240220080000",
            totalAmount: "1234.56",
            modifiedDtm: "20240201100000",
            lines: new[]
            {
                ErpRecordFactory.Line(" SKU-A ", "10", "3", "1.5000"),
                ErpRecordFactory.Line("SKU-B", "4", "", "2.2500"),
            }));

        Assert.True(result.IsSuccess);
        var order = result.Value!;
        Assert.Equal("PO-000001", order.PoNumber);
        Assert.Equal("VND-007", order.VendorCode);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        Assert.Equal(1234.56m, order.TotalAmount);
        Assert.Equal(new DateTime(2024, 2, 1, 10, 0, 0, DateTimeKind.Utc), order.LastSyncedUtc);
        var lines = order.Lines.ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(10, lines[0].QuantityOrdered);
        Assert.Equal(3, lines[0].QuantityReceived);
        Assert.Equal(0, lines[1].QuantityReceived);
    }

    [Fact]
    public void MapPurchaseOrder_UnknownStatus_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order("PO-000001", status: "WAT"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "PO_STATUS");
    }

    [Fact]
    public void MapPurchaseOrder_BadLinePrice_ReturnsLineFieldError()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order(
            "PO-000001",
            lines: new[]
            {
                ErpRecordFactory.Line("SKU-A", "10", "0", "cheap"),
            }));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "PO_LINES[0].POL_UNIT_PRICE");
    }

    [Fact]
    public void MapPurchaseOrder_MissingPoNumber_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order(""));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.FieldName == "PO_NUMBER");
    }
}
