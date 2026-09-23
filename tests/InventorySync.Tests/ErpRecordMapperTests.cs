using FluentAssertions;

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

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        var item = result.Value!;
        item.Sku.Should().Be("SKU-000001");
        item.Name.Should().Be("Widget");
        item.Description.Should().Be("A widget");
        item.QuantityOnHand.Should().Be(123);
        item.UnitCost.Should().Be(45.6789m);
        item.WarehouseCode.Should().Be("WH02");
        item.ErpRecordId.Should().Be("ERP-1");
        item.LastSyncedUtc.Should().Be(new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void MapInventoryItem_TrimsWhitespace_FromAllStringFields()
    {
        var result = ErpRecordMapper.MapInventoryItem(
            ErpRecordFactory.Item("  SKU-000001\t", name: "\tWidget ", warehouseCode: " WH03 ", modifiedDtm: "20240101120000"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Sku.Should().Be("SKU-000001");
        result.Value.Name.Should().Be("Widget");
        result.Value.WarehouseCode.Should().Be("WH03");
    }

    [Fact]
    public void MapInventoryItem_MissingSku_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item(sku: ""));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_SKU");
    }

    [Fact]
    public void MapInventoryItem_WhitespaceOnlySku_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item(sku: "   "));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_SKU");
    }

    [Fact]
    public void MapInventoryItem_NonNumericQuantity_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "N/A"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_QTY_ON_HAND" && e.Message.Contains("N/A"));
    }

    [Fact]
    public void MapInventoryItem_MalformedDate_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", modifiedDtm: "yesterday"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_MOD_DTM");
    }

    [Fact]
    public void MapInventoryItem_WrongDateFormat_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", modifiedDtm: "2024/03/15 10:30:00"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_MOD_DTM");
    }

    [Fact]
    public void MapInventoryItem_DateIsParsedAsUtc()
    {
        var result = ErpRecordMapper.MapInventoryItem(
            ErpRecordFactory.Item("SKU-000001", modifiedDtm: "20240102153045"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.LastSyncedUtc.Should().Be(new DateTime(2024, 1, 2, 15, 30, 45, DateTimeKind.Utc));
        result.Value.LastSyncedUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MapInventoryItem_DecimalParsing_IsCultureInsensitive()
    {
        var currentCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", unitCost: "1234.5678"));

            result.IsSuccess.Should().BeTrue();
            result.Value!.UnitCost.Should().Be(1234.5678m);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = currentCulture;
        }
    }

    [Fact]
    public void MapInventoryItem_CommaDecimal_IsRejectedUnderInvariantCulture()
    {
        var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", unitCost: "12,50"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "ITM_UNIT_COST");
    }

    [Fact]
    public void MapInventoryItem_IntegerQuantity_IsCultureInsensitive()
    {
        var currentCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var result = ErpRecordMapper.MapInventoryItem(ErpRecordFactory.Item("SKU-000001", quantityOnHand: "12345"));

            result.IsSuccess.Should().BeTrue();
            result.Value!.QuantityOnHand.Should().Be(12345);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = currentCulture;
        }
    }

    [Fact]
    public void MapInventoryItem_NullOptionalFields_AreAccepted()
    {
        var result = ErpRecordMapper.MapInventoryItem(
            ErpRecordFactory.Item("SKU-000001", description: null, erpRecordId: null));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().BeNull();
        result.Value.ErpRecordId.Should().BeNull();
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

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        var order = result.Value!;
        order.PoNumber.Should().Be("PO-000001");
        order.VendorCode.Should().Be("VND-007");
        order.Status.Should().Be(PurchaseOrderStatus.Submitted);
        order.TotalAmount.Should().Be(1234.56m);
        order.LastSyncedUtc.Should().Be(new DateTime(2024, 2, 1, 10, 0, 0, DateTimeKind.Utc));

        var lines = order.Lines.ToList();
        lines.Should().HaveCount(2);
        lines[0].QuantityOrdered.Should().Be(10);
        lines[0].QuantityReceived.Should().Be(3);
        lines[1].QuantityReceived.Should().Be(0);
    }

    [Fact]
    public void MapPurchaseOrder_UnknownStatus_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order("PO-000001", status: "WAT"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "PO_STATUS");
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

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "PO_LINES[0].POL_UNIT_PRICE");
    }

    [Fact]
    public void MapPurchaseOrder_MissingPoNumber_ReturnsFieldError()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order(""));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.FieldName == "PO_NUMBER");
    }

    [Fact]
    public void MapPurchaseOrder_MultipleFieldErrors_AreAllReported()
    {
        var result = ErpRecordMapper.MapPurchaseOrder(ErpRecordFactory.Order(
            "",
            vendorCode: "",
            status: "NOPE",
            orderDateDtm: "bad-date",
            modifiedDtm: "20240101120000",
            expectedDateDtm: "20240105120000"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Select(e => e.FieldName).Should().Contain("PO_NUMBER", "PO_VENDOR", "PO_STATUS", "PO_ORDER_DTM");
    }
}
