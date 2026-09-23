using System.Globalization;
using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Exceptions;
using InventorySync.Core.Interfaces;

namespace InventorySync.Infrastructure.Services;

public class SyncService : ISyncService
{
    private const int BatchSize = 500;

    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISyncRepository _syncRepository;
    private readonly IErpConnector _erpConnector;

    public SyncService(
        IInventoryItemRepository inventoryRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ISyncRepository syncRepository,
        IErpConnector erpConnector)
    {
        _inventoryRepository = inventoryRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _syncRepository = syncRepository;
        _erpConnector = erpConnector;
    }

    public async Task<SyncResultDto> SyncInventoryAsync(string triggeredBy, CancellationToken ct = default)
    {
        var run = await BeginRunAsync(SyncEntityType.InventoryItem, triggeredBy, ct);
        var auditBuffer = new List<SyncAuditEntry>();

        try
        {
            var batch = new List<ErpInventoryRecord>(BatchSize);
            await foreach (var record in _erpConnector.GetInventoryRecordsAsync(ct).WithCancellation(ct))
            {
                batch.Add(record);
                if (batch.Count >= BatchSize)
                {
                    await ProcessInventoryBatchAsync(run, batch, auditBuffer, ct);
                    batch = new List<ErpInventoryRecord>(BatchSize);
                }
            }

            if (batch.Count > 0)
            {
                await ProcessInventoryBatchAsync(run, batch, auditBuffer, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(run, ct);
            throw;
        }

        await CompleteRunAsync(run, ct);
        return DtoMapper.ToSyncResult(run);
    }

    public async Task<SyncResultDto> SyncPurchaseOrdersAsync(string triggeredBy, CancellationToken ct = default)
    {
        var run = await BeginRunAsync(SyncEntityType.PurchaseOrder, triggeredBy, ct);
        var auditBuffer = new List<SyncAuditEntry>();

        try
        {
            var batch = new List<ErpPurchaseOrderRecord>(BatchSize);
            await foreach (var record in _erpConnector.GetPurchaseOrderRecordsAsync(ct).WithCancellation(ct))
            {
                batch.Add(record);
                if (batch.Count >= BatchSize)
                {
                    await ProcessPurchaseOrderBatchAsync(run, batch, auditBuffer, ct);
                    batch = new List<ErpPurchaseOrderRecord>(BatchSize);
                }
            }

            if (batch.Count > 0)
            {
                await ProcessPurchaseOrderBatchAsync(run, batch, auditBuffer, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(run, ct);
            throw;
        }

        await CompleteRunAsync(run, ct);
        return DtoMapper.ToSyncResult(run);
    }

    public async Task<PagedResult<SyncRunDto>> GetRunsAsync(
        SyncEntityType? entityType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var result = await _syncRepository.GetRunsAsync(entityType, page, pageSize, ct);

        return new PagedResult<SyncRunDto>
        {
            Items = result.Items.Select(DtoMapper.ToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IReadOnlyList<SyncAuditEntryDto>> GetAuditEntriesAsync(
        int syncRunId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (await _syncRepository.GetRunByIdAsync(syncRunId, ct) is null)
        {
            throw new EntityNotFoundException($"Sync run with id {syncRunId} was not found.");
        }

        var entries = await _syncRepository.GetAuditEntriesAsync(syncRunId, (page - 1) * pageSize, pageSize, ct);
        return entries.Select(DtoMapper.ToDto).ToList();
    }

    private async Task<SyncRun> BeginRunAsync(SyncEntityType entityType, string triggeredBy, CancellationToken ct)
    {
        var run = new SyncRun
        {
            EntityType = entityType,
            StartedUtc = DateTime.UtcNow,
            Status = SyncRunStatus.Running,
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "api" : triggeredBy,
        };

        await _syncRepository.AddRunAsync(run, ct);
        await _syncRepository.SaveChangesAsync(ct);

        return run;
    }

    private async Task CompleteRunAsync(SyncRun run, CancellationToken ct)
    {
        run.CompletedUtc = DateTime.UtcNow;
        run.Status = run.RecordsFailed > 0 ? SyncRunStatus.PartialSuccess : SyncRunStatus.Succeeded;
        await _syncRepository.SaveChangesAsync(ct);
    }

    private async Task FailRunAsync(SyncRun run, CancellationToken ct)
    {
        run.CompletedUtc = DateTime.UtcNow;
        run.Status = SyncRunStatus.Failed;
        await _syncRepository.SaveChangesAsync(ct);
    }

    private async Task ProcessInventoryBatchAsync(
        SyncRun run,
        IReadOnlyList<ErpInventoryRecord> batch,
        List<SyncAuditEntry> auditBuffer,
        CancellationToken ct)
    {
        var skus = batch.Select(r => r.Sku).Distinct().ToList();
        var existing = await _inventoryRepository.GetBySkusAsync(skus, ct);

        foreach (var record in batch)
        {
            run.RecordsRead++;

            try
            {
                if (existing.TryGetValue(record.Sku, out var item))
                {
                    ApplyInventoryChanges(item, record, run, auditBuffer);
                }
                else
                {
                    item = ToEntity(record);
                    await _inventoryRepository.AddAsync(item, ct);
                    run.RecordsInserted++;
                    auditBuffer.Add(CreateAudit(
                        run.Id,
                        SyncEntityType.InventoryItem,
                        record.Sku,
                        SyncAuditAction.Insert,
                        fieldName: null,
                        oldValue: null,
                        newValue: null,
                        message: null));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.RecordsFailed++;
                auditBuffer.Add(CreateAudit(
                    run.Id,
                    SyncEntityType.InventoryItem,
                    record.Sku,
                    SyncAuditAction.Error,
                    fieldName: null,
                    oldValue: null,
                    newValue: null,
                    message: ex.Message));
            }
        }

        await _inventoryRepository.SaveChangesAsync(ct);
        FlushAudits(auditBuffer);
        await _syncRepository.SaveChangesAsync(ct);
    }

    private async Task ProcessPurchaseOrderBatchAsync(
        SyncRun run,
        IReadOnlyList<ErpPurchaseOrderRecord> batch,
        List<SyncAuditEntry> auditBuffer,
        CancellationToken ct)
    {
        var poNumbers = batch.Select(r => r.PoNumber).Distinct().ToList();
        var existing = await _purchaseOrderRepository.GetByPoNumbersAsync(poNumbers, ct);

        foreach (var record in batch)
        {
            run.RecordsRead++;

            try
            {
                if (existing.TryGetValue(record.PoNumber, out var order))
                {
                    ApplyPurchaseOrderChanges(order, record, run, auditBuffer);
                }
                else
                {
                    order = ToEntity(record);
                    await _purchaseOrderRepository.AddAsync(order, ct);
                    run.RecordsInserted++;
                    auditBuffer.Add(CreateAudit(
                        run.Id,
                        SyncEntityType.PurchaseOrder,
                        record.PoNumber,
                        SyncAuditAction.Insert,
                        fieldName: null,
                        oldValue: null,
                        newValue: null,
                        message: $"Inserted purchase order with {record.Lines.Count} lines."));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.RecordsFailed++;
                auditBuffer.Add(CreateAudit(
                    run.Id,
                    SyncEntityType.PurchaseOrder,
                    record.PoNumber,
                    SyncAuditAction.Error,
                    fieldName: null,
                    oldValue: null,
                    newValue: null,
                    message: ex.Message));
            }
        }

        await _purchaseOrderRepository.SaveChangesAsync(ct);
        FlushAudits(auditBuffer);
        await _syncRepository.SaveChangesAsync(ct);
    }

    private static void ApplyInventoryChanges(
        InventoryItem item,
        ErpInventoryRecord record,
        SyncRun run,
        List<SyncAuditEntry> auditBuffer)
    {
        var timestamp = DateTime.UtcNow;
        var changed = false;

        if (!string.Equals(item.Name, record.Name, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "Name", item.Name, record.Name, timestamp);
            item.Name = record.Name;
            changed = true;
        }

        if (!string.Equals(item.Description, record.Description, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "Description", item.Description, record.Description, timestamp);
            item.Description = record.Description;
            changed = true;
        }

        if (item.QuantityOnHand != record.QuantityOnHand)
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "QuantityOnHand", ToAuditValue(item.QuantityOnHand), ToAuditValue(record.QuantityOnHand), timestamp);
            item.QuantityOnHand = record.QuantityOnHand;
            changed = true;
        }

        if (item.UnitCost != record.UnitCost)
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "UnitCost", ToAuditValue(item.UnitCost), ToAuditValue(record.UnitCost), timestamp);
            item.UnitCost = record.UnitCost;
            changed = true;
        }

        if (!string.Equals(item.WarehouseCode, record.WarehouseCode, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "WarehouseCode", item.WarehouseCode, record.WarehouseCode, timestamp);
            item.WarehouseCode = record.WarehouseCode;
            changed = true;
        }

        if (!string.Equals(item.ErpRecordId, record.ErpRecordId, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, record.Sku, "ErpRecordId", item.ErpRecordId, record.ErpRecordId, timestamp);
            item.ErpRecordId = record.ErpRecordId;
            changed = true;
        }

        if (changed)
        {
            item.LastSyncedUtc = record.ModifiedUtc;
            run.RecordsUpdated++;
        }
        else
        {
            auditBuffer.Add(CreateAudit(
                run.Id,
                SyncEntityType.InventoryItem,
                record.Sku,
                SyncAuditAction.Skip,
                fieldName: null,
                oldValue: null,
                newValue: null,
                message: null,
                timestamp));
        }
    }

    private static void ApplyPurchaseOrderChanges(
        PurchaseOrder order,
        ErpPurchaseOrderRecord record,
        SyncRun run,
        List<SyncAuditEntry> auditBuffer)
    {
        var timestamp = DateTime.UtcNow;
        var entityType = SyncEntityType.PurchaseOrder;
        var entityKey = record.PoNumber;
        var changed = false;

        var effectiveStatus = StatusRank(record.Status) >= StatusRank(order.Status) ? record.Status : order.Status;
        if (effectiveStatus != order.Status)
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "Status", order.Status.ToString(), effectiveStatus.ToString(), timestamp);
            order.Status = effectiveStatus;
            changed = true;
        }

        if (effectiveStatus != record.Status)
        {
            AddFieldAudit(
                auditBuffer,
                run.Id,
                entityType,
                entityKey,
                "Status",
                record.Status.ToString(),
                effectiveStatus.ToString(),
                timestamp,
                SyncAuditAction.ConflictResolved,
                "The local status is further along than the ERP status; the local status was kept.");
        }

        if (!string.Equals(order.VendorCode, record.VendorCode, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "VendorCode", order.VendorCode, record.VendorCode, timestamp);
            order.VendorCode = record.VendorCode;
            changed = true;
        }

        if (order.OrderDateUtc != record.OrderDateUtc)
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "OrderDateUtc", ToAuditValue(order.OrderDateUtc), ToAuditValue(record.OrderDateUtc), timestamp);
            order.OrderDateUtc = record.OrderDateUtc;
            changed = true;
        }

        if (order.ExpectedDateUtc != record.ExpectedDateUtc)
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "ExpectedDateUtc", ToAuditValue(order.ExpectedDateUtc), ToAuditValue(record.ExpectedDateUtc), timestamp);
            order.ExpectedDateUtc = record.ExpectedDateUtc;
            changed = true;
        }

        if (order.TotalAmount != record.TotalAmount)
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "TotalAmount", ToAuditValue(order.TotalAmount), ToAuditValue(record.TotalAmount), timestamp);
            order.TotalAmount = record.TotalAmount;
            changed = true;
        }

        if (!string.Equals(order.ErpRecordId, record.ErpRecordId, StringComparison.Ordinal))
        {
            AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, "ErpRecordId", order.ErpRecordId, record.ErpRecordId, timestamp);
            order.ErpRecordId = record.ErpRecordId;
            changed = true;
        }

        var existingBySku = order.Lines.ToDictionary(l => l.Sku, StringComparer.Ordinal);

        foreach (var erpLine in record.Lines)
        {
            if (existingBySku.Remove(erpLine.Sku, out var line))
            {
                if (line.QuantityOrdered != erpLine.QuantityOrdered)
                {
                    AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, $"Lines/{erpLine.Sku}/QuantityOrdered", ToAuditValue(line.QuantityOrdered), ToAuditValue(erpLine.QuantityOrdered), timestamp);
                    line.QuantityOrdered = erpLine.QuantityOrdered;
                    changed = true;
                }

                if (line.UnitPrice != erpLine.UnitPrice)
                {
                    AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, $"Lines/{erpLine.Sku}/UnitPrice", ToAuditValue(line.UnitPrice), ToAuditValue(erpLine.UnitPrice), timestamp);
                    line.UnitPrice = erpLine.UnitPrice;
                    changed = true;
                }

                var received = Math.Max(line.QuantityReceived, erpLine.QuantityReceived);
                if (received != line.QuantityReceived)
                {
                    line.QuantityReceived = received;
                    changed = true;
                }

                if (received != erpLine.QuantityReceived)
                {
                    AddFieldAudit(
                        auditBuffer,
                        run.Id,
                        entityType,
                        entityKey,
                        $"Lines/{erpLine.Sku}/QuantityReceived",
                        ToAuditValue(erpLine.QuantityReceived),
                        ToAuditValue(received),
                        timestamp,
                        SyncAuditAction.ConflictResolved,
                        "Locally recorded receipts exceed the ERP value; the local value was preserved.");
                }
            }
            else
            {
                order.Lines.Add(new PurchaseOrderLine
                {
                    Sku = erpLine.Sku,
                    QuantityOrdered = erpLine.QuantityOrdered,
                    QuantityReceived = erpLine.QuantityReceived,
                    UnitPrice = erpLine.UnitPrice,
                });
                changed = true;
                AddFieldAudit(
                    auditBuffer,
                    run.Id,
                    entityType,
                    entityKey,
                    "Lines",
                    oldValue: null,
                    newValue: erpLine.Sku,
                    timestamp,
                    action: SyncAuditAction.Update,
                    message: $"Added line {erpLine.Sku} with quantity {erpLine.QuantityOrdered}.");
            }
        }

        foreach (var removed in existingBySku.Values)
        {
            order.Lines.Remove(removed);
            changed = true;
            AddFieldAudit(
                auditBuffer,
                run.Id,
                entityType,
                entityKey,
                "Lines",
                removed.Sku,
                newValue: null,
                timestamp,
                action: SyncAuditAction.Update,
                message: $"Removed line {removed.Sku}.");
        }

        if (changed)
        {
            order.LastSyncedUtc = record.ModifiedUtc;
            run.RecordsUpdated++;
        }
        else
        {
            auditBuffer.Add(CreateAudit(
                run.Id,
                entityType,
                entityKey,
                SyncAuditAction.Skip,
                fieldName: null,
                oldValue: null,
                newValue: null,
                message: null,
                timestamp));
        }
    }

    private static InventoryItem ToEntity(ErpInventoryRecord record)
    {
        return new InventoryItem
        {
            Sku = record.Sku,
            Name = record.Name,
            Description = record.Description,
            QuantityOnHand = record.QuantityOnHand,
            UnitCost = record.UnitCost,
            WarehouseCode = record.WarehouseCode,
            ErpRecordId = record.ErpRecordId,
            LastSyncedUtc = record.ModifiedUtc,
        };
    }

    private static PurchaseOrder ToEntity(ErpPurchaseOrderRecord record)
    {
        return new PurchaseOrder
        {
            PoNumber = record.PoNumber,
            VendorCode = record.VendorCode,
            Status = record.Status,
            OrderDateUtc = record.OrderDateUtc,
            ExpectedDateUtc = record.ExpectedDateUtc,
            TotalAmount = record.TotalAmount,
            ErpRecordId = record.ErpRecordId,
            LastSyncedUtc = record.ModifiedUtc,
            Lines = record.Lines.Select(l => new PurchaseOrderLine
            {
                Sku = l.Sku,
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
    }

    private static int StatusRank(PurchaseOrderStatus status)
    {
        return status switch
        {
            PurchaseOrderStatus.Draft => 0,
            PurchaseOrderStatus.Submitted => 1,
            PurchaseOrderStatus.PartiallyReceived => 2,
            PurchaseOrderStatus.Received => 3,
            PurchaseOrderStatus.Cancelled => 4,
            _ => 0,
        };
    }

    private static SyncAuditEntry CreateAudit(
        int syncRunId,
        SyncEntityType entityType,
        string entityKey,
        SyncAuditAction action,
        string? fieldName,
        string? oldValue,
        string? newValue,
        string? message,
        DateTime? timestamp = null)
    {
        return new SyncAuditEntry
        {
            SyncRunId = syncRunId,
            EntityType = entityType,
            EntityKey = entityKey,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Message = message,
            TimestampUtc = timestamp ?? DateTime.UtcNow,
        };
    }

    private static void AddFieldAudit(
        List<SyncAuditEntry> auditBuffer,
        int syncRunId,
        SyncEntityType entityType,
        string entityKey,
        string fieldName,
        string? oldValue,
        string? newValue,
        DateTime timestamp,
        SyncAuditAction action = SyncAuditAction.Update,
        string? message = null)
    {
        auditBuffer.Add(CreateAudit(syncRunId, entityType, entityKey, action, fieldName, oldValue, newValue, message, timestamp));
    }

    private static string ToAuditValue(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToAuditValue(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToAuditValue(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private void FlushAudits(List<SyncAuditEntry> auditBuffer)
    {
        if (auditBuffer.Count == 0)
        {
            return;
        }

        _syncRepository.AddAuditEntries(auditBuffer);
        auditBuffer.Clear();
    }
}
