using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;

namespace InventorySync.Infrastructure.Services;

internal static class DtoMapper
{
    public static InventoryItemDto ToDto(InventoryItem item)
    {
        return new InventoryItemDto
        {
            Id = item.Id,
            Sku = item.Sku,
            Name = item.Name,
            Description = item.Description,
            QuantityOnHand = item.QuantityOnHand,
            UnitCost = item.UnitCost,
            WarehouseCode = item.WarehouseCode,
            LastSyncedUtc = item.LastSyncedUtc,
            LocallyModifiedUtc = item.LocallyModifiedUtc,
            ErpRecordId = item.ErpRecordId,
            RowVersion = item.RowVersion is null ? null : Convert.ToBase64String(item.RowVersion),
        };
    }

    public static PurchaseOrderDto ToDto(PurchaseOrder order)
    {
        return new PurchaseOrderDto
        {
            Id = order.Id,
            PoNumber = order.PoNumber,
            VendorCode = order.VendorCode,
            Status = order.Status,
            OrderDateUtc = order.OrderDateUtc,
            ExpectedDateUtc = order.ExpectedDateUtc,
            TotalAmount = order.TotalAmount,
            ErpRecordId = order.ErpRecordId,
            LastSyncedUtc = order.LastSyncedUtc,
            LocallyModifiedUtc = order.LocallyModifiedUtc,
            Lines = order.Lines.Select(ToDto).ToList(),
        };
    }

    public static PurchaseOrderLineDto ToDto(PurchaseOrderLine line)
    {
        return new PurchaseOrderLineDto
        {
            Id = line.Id,
            Sku = line.Sku,
            QuantityOrdered = line.QuantityOrdered,
            QuantityReceived = line.QuantityReceived,
            UnitPrice = line.UnitPrice,
        };
    }

    public static SyncRunDto ToDto(SyncRun run)
    {
        return new SyncRunDto
        {
            Id = run.Id,
            ParentSyncRunId = run.ParentSyncRunId,
            EntityType = run.EntityType,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            Status = run.Status,
            RecordsRead = run.RecordsRead,
            RecordsInserted = run.RecordsInserted,
            RecordsUpdated = run.RecordsUpdated,
            RecordsFailed = run.RecordsFailed,
            TriggeredBy = run.TriggeredBy,
        };
    }

    public static SyncAuditEntryDto ToDto(SyncAuditEntry entry)
    {
        return new SyncAuditEntryDto
        {
            Id = entry.Id,
            SyncRunId = entry.SyncRunId,
            EntityType = entry.EntityType,
            EntityKey = entry.EntityKey,
            Action = entry.Action,
            FieldName = entry.FieldName,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Message = entry.Message,
            TimestampUtc = entry.TimestampUtc,
        };
    }
}
