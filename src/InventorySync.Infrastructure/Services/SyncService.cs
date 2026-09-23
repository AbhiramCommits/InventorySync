using System.Globalization;

using InventorySync.Core.Dtos;
using InventorySync.Core.Dtos.Erp;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Exceptions;
using InventorySync.Core.Interfaces;
using InventorySync.Core.Mapping;
using InventorySync.Core.Options;
using InventorySync.Infrastructure.Erp;

using Microsoft.Extensions.Options;

namespace InventorySync.Infrastructure.Services;

public class SyncService : ISyncService
{
    private const string ConflictMessage =
        "The record had local changes and the ERP value differs; the conflict was resolved per the configured strategy.";

    private const string UnknownKey = "(unknown)";

    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISyncRepository _syncRepository;
    private readonly IErpClient _erpClient;
    private readonly IOptions<SyncOptions> _options;

    public SyncService(
        IInventoryItemRepository inventoryRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ISyncRepository syncRepository,
        IErpClient erpClient,
        IOptions<SyncOptions> options)
    {
        _inventoryRepository = inventoryRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _syncRepository = syncRepository;
        _erpClient = erpClient;
        _options = options;
    }

    private SyncOptions Options => _options.Value;

    public async Task<SyncRunDto> SyncInventoryAsync(string triggeredBy, CancellationToken ct = default)
    {
        var run = await BeginRunAsync(SyncEntityType.InventoryItem, triggeredBy, null, ct);

        try
        {
            var records = await _erpClient.GetInventoryAsync(null, ct);
            await ExecuteInventorySyncAsync(run, records, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(run, ct);
            throw;
        }

        await CompleteRunAsync(run, ct);
        return DtoMapper.ToDto(run);
    }

    public async Task<SyncRunDto> SyncPurchaseOrdersAsync(string triggeredBy, CancellationToken ct = default)
    {
        var run = await BeginRunAsync(SyncEntityType.PurchaseOrder, triggeredBy, null, ct);

        try
        {
            var records = await _erpClient.GetPurchaseOrdersAsync(null, ct);
            await ExecutePurchaseOrderSyncAsync(run, records, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(run, ct);
            throw;
        }

        await CompleteRunAsync(run, ct);
        return DtoMapper.ToDto(run);
    }

    public async Task<SyncRunDto> RetryFailedRecordsAsync(int syncRunId, CancellationToken ct = default)
    {
        var parent = await _syncRepository.GetRunByIdAsync(syncRunId, ct)
            ?? throw new EntityNotFoundException($"Sync run with id {syncRunId} was not found.");

        if (parent.ParentSyncRunId is not null)
        {
            throw new InvalidOperationException($"Run {syncRunId} is itself a retry run and cannot be retried.");
        }

        var errors = await _syncRepository.GetAuditEntriesByActionAsync(parent.Id, SyncAuditAction.Error, ct);
        var keys = errors
            .Select(e => e.EntityKey)
            .Where(k => !string.IsNullOrWhiteSpace(k) && !string.Equals(k, UnknownKey, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var child = await BeginRunAsync(parent.EntityType, parent.TriggeredBy, parent.Id, ct);

        try
        {
            if (keys.Count > 0)
            {
                if (parent.EntityType == SyncEntityType.InventoryItem)
                {
                    var records = await _erpClient.GetInventoryBySkusAsync(keys, ct);
                    await ExecuteInventorySyncAsync(child, records, ct);
                }
                else
                {
                    var records = await _erpClient.GetPurchaseOrdersByNumbersAsync(keys, ct);
                    await ExecutePurchaseOrderSyncAsync(child, records, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(child, ct);
            throw;
        }

        await CompleteRunAsync(child, ct);
        return DtoMapper.ToDto(child);
    }

    public async Task<PagedResult<SyncRunDto>> GetRunsAsync(
        SyncEntityType? entityType,
        SyncRunStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var result = await _syncRepository.GetRunsAsync(entityType, status, page, pageSize, ct);

        return new PagedResult<SyncRunDto>
        {
            Items = result.Items.Select(DtoMapper.ToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<SyncRunDto> GetRunByIdAsync(int id, CancellationToken ct = default)
    {
        var run = await _syncRepository.GetRunByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Sync run with id {id} was not found.");

        return DtoMapper.ToDto(run);
    }

    public async Task<PagedResult<SyncAuditEntryDto>> GetAuditEntriesAsync(
        int syncRunId,
        SyncAuditAction? action,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (await _syncRepository.GetRunByIdAsync(syncRunId, ct) is null)
        {
            throw new EntityNotFoundException($"Sync run with id {syncRunId} was not found.");
        }

        var totalCount = await _syncRepository.CountAuditEntriesAsync(syncRunId, action, ct);
        var entries = await _syncRepository.GetAuditEntriesAsync(syncRunId, action, (page - 1) * pageSize, pageSize, ct);

        return new PagedResult<SyncAuditEntryDto>
        {
            Items = entries.Select(DtoMapper.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    private async Task<SyncRun> BeginRunAsync(SyncEntityType entityType, string triggeredBy, int? parentSyncRunId, CancellationToken ct)
    {
        var run = new SyncRun
        {
            ParentSyncRunId = parentSyncRunId,
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

    private async Task ExecuteInventorySyncAsync(SyncRun run, IReadOnlyList<ErpInventoryItemRecord> records, CancellationToken ct)
    {
        var auditBuffer = new List<SyncAuditEntry>();

        foreach (var batch in records.Chunk(Math.Max(1, Options.PageSize)))
        {
            var mapped = batch
                .Select(record => (Record: record, Result: ErpRecordMapper.MapInventoryItem(record)))
                .ToList();

            foreach (var entry in mapped.Where(m => !m.Result.IsSuccess))
            {
                run.RecordsRead++;
                run.RecordsFailed++;
                var key = string.IsNullOrWhiteSpace(entry.Record.Sku) ? UnknownKey : entry.Record.Sku.Trim();
                auditBuffer.Add(CreateAudit(
                    run.Id,
                    SyncEntityType.InventoryItem,
                    key,
                    SyncAuditAction.Error,
                    fieldName: null,
                    oldValue: null,
                    newValue: null,
                    message: FormatFieldErrors(entry.Result.Errors)));
            }

            var skus = mapped
                .Where(m => m.Result.IsSuccess)
                .Select(m => m.Result.Value!.Sku)
                .ToList();

            var existing = await _inventoryRepository.GetBySkusAsync(skus, ct);

            foreach (var entry in mapped.Where(m => m.Result.IsSuccess))
            {
                run.RecordsRead++;
                var mappedItem = entry.Result.Value!;

                try
                {
                    if (existing.TryGetValue(mappedItem.Sku, out var item))
                    {
                        ApplyInventoryChanges(item, mappedItem, run, auditBuffer);
                    }
                    else
                    {
                        await _inventoryRepository.AddAsync(mappedItem, ct);
                        run.RecordsInserted++;
                        auditBuffer.Add(CreateAudit(
                            run.Id,
                            SyncEntityType.InventoryItem,
                            mappedItem.Sku,
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
                        mappedItem.Sku,
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
    }

    private async Task ExecutePurchaseOrderSyncAsync(SyncRun run, IReadOnlyList<ErpPurchaseOrderRecord> records, CancellationToken ct)
    {
        var auditBuffer = new List<SyncAuditEntry>();

        foreach (var batch in records.Chunk(Math.Max(1, Options.PageSize)))
        {
            var mapped = batch
                .Select(record => (Record: record, Result: ErpRecordMapper.MapPurchaseOrder(record)))
                .ToList();

            foreach (var entry in mapped.Where(m => !m.Result.IsSuccess))
            {
                run.RecordsRead++;
                run.RecordsFailed++;
                var key = string.IsNullOrWhiteSpace(entry.Record.PoNumber) ? UnknownKey : entry.Record.PoNumber.Trim();
                auditBuffer.Add(CreateAudit(
                    run.Id,
                    SyncEntityType.PurchaseOrder,
                    key,
                    SyncAuditAction.Error,
                    fieldName: null,
                    oldValue: null,
                    newValue: null,
                    message: FormatFieldErrors(entry.Result.Errors)));
            }

            var poNumbers = mapped
                .Where(m => m.Result.IsSuccess)
                .Select(m => m.Result.Value!.PoNumber)
                .ToList();

            var existing = await _purchaseOrderRepository.GetByPoNumbersAsync(poNumbers, ct);

            foreach (var entry in mapped.Where(m => m.Result.IsSuccess))
            {
                run.RecordsRead++;
                var mappedOrder = entry.Result.Value!;

                try
                {
                    if (existing.TryGetValue(mappedOrder.PoNumber, out var order))
                    {
                        ApplyPurchaseOrderChanges(order, mappedOrder, run, auditBuffer);
                    }
                    else
                    {
                        await _purchaseOrderRepository.AddAsync(mappedOrder, ct);
                        run.RecordsInserted++;
                        auditBuffer.Add(CreateAudit(
                            run.Id,
                            SyncEntityType.PurchaseOrder,
                            mappedOrder.PoNumber,
                            SyncAuditAction.Insert,
                            fieldName: null,
                            oldValue: null,
                            newValue: null,
                            message: $"Inserted purchase order with {mappedOrder.Lines.Count} lines."));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    run.RecordsFailed++;
                    auditBuffer.Add(CreateAudit(
                        run.Id,
                        SyncEntityType.PurchaseOrder,
                        mappedOrder.PoNumber,
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
    }

    private void ApplyInventoryChanges(InventoryItem item, InventoryItem mapped, SyncRun run, List<SyncAuditEntry> auditBuffer)
    {
        var timestamp = DateTime.UtcNow;
        var erpModifiedUtc = mapped.LastSyncedUtc ?? timestamp;
        var erpApplied = false;
        var localKept = false;

        void SyncField<T>(string fieldName, T localValue, T erpValue, Action<T> setter, Func<T, string?> format)
        {
            if (EqualityComparer<T>.Default.Equals(localValue, erpValue))
            {
                return;
            }

            var oldText = format(localValue);
            var newText = format(erpValue);

            if (item.LocallyModifiedUtc is null)
            {
                setter(erpValue);

                erpApplied = true;
                AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, item.Sku, fieldName, oldText, newText, timestamp);
            }
            else if (ShouldErpWin(erpModifiedUtc, item.LocallyModifiedUtc))
            {
                setter(erpValue);

                erpApplied = true;
                AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, item.Sku, fieldName, oldText, newText, timestamp, SyncAuditAction.ConflictResolved, ConflictMessage);
            }
            else
            {
                localKept = true;
                AddFieldAudit(auditBuffer, run.Id, SyncEntityType.InventoryItem, item.Sku, fieldName, newText, oldText, timestamp, SyncAuditAction.ConflictResolved, ConflictMessage);
            }
        }

        SyncField("Name", item.Name, mapped.Name, v => item.Name = v, v => v);
        SyncField("Description", item.Description, mapped.Description, v => item.Description = v, v => v);
        SyncField("QuantityOnHand", item.QuantityOnHand, mapped.QuantityOnHand, v => item.QuantityOnHand = v, ToAuditValue);
        SyncField("UnitCost", item.UnitCost, mapped.UnitCost, v => item.UnitCost = v, ToAuditValue);
        SyncField("WarehouseCode", item.WarehouseCode, mapped.WarehouseCode, v => item.WarehouseCode = v, v => v);
        SyncField("ErpRecordId", item.ErpRecordId, mapped.ErpRecordId, v => item.ErpRecordId = v, v => v);

        if (erpApplied)
        {
            item.LastSyncedUtc = mapped.LastSyncedUtc;
            item.LocallyModifiedUtc = null;
            run.RecordsUpdated++;
        }
        else if (localKept)
        {
            item.LastSyncedUtc = mapped.LastSyncedUtc;
            run.RecordsUpdated++;
        }
        else
        {
            auditBuffer.Add(CreateAudit(
                run.Id,
                SyncEntityType.InventoryItem,
                item.Sku,
                SyncAuditAction.Skip,
                fieldName: null,
                oldValue: null,
                newValue: null,
                message: null,
                timestamp));
        }
    }

    private void ApplyPurchaseOrderChanges(PurchaseOrder order, PurchaseOrder mapped, SyncRun run, List<SyncAuditEntry> auditBuffer)
    {
        var timestamp = DateTime.UtcNow;
        var erpModifiedUtc = mapped.LastSyncedUtc ?? timestamp;
        var entityType = SyncEntityType.PurchaseOrder;
        var entityKey = order.PoNumber;
        var erpApplied = false;
        var localKept = false;

        void SyncField<T>(string fieldName, T localValue, T erpValue, Action<T> setter, Func<T, string?> format)
        {
            if (EqualityComparer<T>.Default.Equals(localValue, erpValue))
            {
                return;
            }

            var oldText = format(localValue);
            var newText = format(erpValue);

            if (order.LocallyModifiedUtc is null)
            {
                setter(erpValue);

                erpApplied = true;
                AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, fieldName, oldText, newText, timestamp);
            }
            else if (ShouldErpWin(erpModifiedUtc, order.LocallyModifiedUtc))
            {
                setter(erpValue);

                erpApplied = true;
                AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, fieldName, oldText, newText, timestamp, SyncAuditAction.ConflictResolved, ConflictMessage);
            }
            else
            {
                localKept = true;
                AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, fieldName, newText, oldText, timestamp, SyncAuditAction.ConflictResolved, ConflictMessage);
            }
        }

        SyncField("VendorCode", order.VendorCode, mapped.VendorCode, v => order.VendorCode = v, v => v);
        SyncField("OrderDateUtc", order.OrderDateUtc, mapped.OrderDateUtc, v => order.OrderDateUtc = v, ToAuditValue);
        SyncField("ExpectedDateUtc", order.ExpectedDateUtc, mapped.ExpectedDateUtc, v => order.ExpectedDateUtc = v, ToAuditValue);
        SyncField("TotalAmount", order.TotalAmount, mapped.TotalAmount, v => order.TotalAmount = v, ToAuditValue);
        SyncField("ErpRecordId", order.ErpRecordId, mapped.ErpRecordId, v => order.ErpRecordId = v, v => v);

        if (mapped.Status != order.Status)
        {
            var previousStatus = order.Status;

            if (StatusRank(mapped.Status) >= StatusRank(order.Status))
            {
                SyncField("Status", order.Status, mapped.Status, v => order.Status = v, v => v.ToString());
            }
            else if (Options.ConflictResolution == ConflictResolutionStrategy.ErpWins
                || (Options.ConflictResolution == ConflictResolutionStrategy.NewerWins
                    && (order.LocallyModifiedUtc is null || erpModifiedUtc >= order.LocallyModifiedUtc.Value)))
            {
                order.Status = mapped.Status;

                erpApplied = true;
                AddFieldAudit(
                    auditBuffer,
                    run.Id,
                    entityType,
                    entityKey,
                    "Status",
                    previousStatus.ToString(),
                    mapped.Status.ToString(),
                    timestamp,
                    SyncAuditAction.ConflictResolved,
                    "The ERP status overrides the further-progressed local status per the configured strategy.");
            }
            else
            {
                localKept = true;
                AddFieldAudit(
                    auditBuffer,
                    run.Id,
                    entityType,
                    entityKey,
                    "Status",
                    mapped.Status.ToString(),
                    previousStatus.ToString(),
                    timestamp,
                    SyncAuditAction.ConflictResolved,
                    "The local status is further along than the ERP status; the local status was kept.");
            }
        }

        var existingBySku = order.Lines.ToDictionary(l => l.Sku, StringComparer.Ordinal);

        foreach (var erpLine in mapped.Lines)
        {
            if (existingBySku.Remove(erpLine.Sku, out var line))
            {
                if (line.QuantityOrdered != erpLine.QuantityOrdered)
                {
                    AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, $"Lines/{erpLine.Sku}/QuantityOrdered", ToAuditValue(line.QuantityOrdered), ToAuditValue(erpLine.QuantityOrdered), timestamp);
                    line.QuantityOrdered = erpLine.QuantityOrdered;
                    erpApplied = true;
                }

                if (line.UnitPrice != erpLine.UnitPrice)
                {
                    AddFieldAudit(auditBuffer, run.Id, entityType, entityKey, $"Lines/{erpLine.Sku}/UnitPrice", ToAuditValue(line.UnitPrice), ToAuditValue(erpLine.UnitPrice), timestamp);
                    line.UnitPrice = erpLine.UnitPrice;
                    erpApplied = true;
                }

                var received = Math.Max(line.QuantityReceived, erpLine.QuantityReceived);
                if (received != line.QuantityReceived)
                {
                    line.QuantityReceived = received;
                    erpApplied = true;
                }

                if (received != erpLine.QuantityReceived)
                {
                    localKept = true;
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

                erpApplied = true;
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
            erpApplied = true;
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

        if (erpApplied)
        {
            order.LastSyncedUtc = mapped.LastSyncedUtc;
            order.LocallyModifiedUtc = null;
            run.RecordsUpdated++;
        }
        else if (localKept)
        {
            order.LastSyncedUtc = mapped.LastSyncedUtc;
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

    private bool ShouldErpWin(DateTime erpModifiedUtc, DateTime? locallyModifiedUtc)
    {
        return Options.ConflictResolution switch
        {
            ConflictResolutionStrategy.ErpWins => true,
            ConflictResolutionStrategy.LocalWins => false,
            _ => locallyModifiedUtc is null || erpModifiedUtc >= locallyModifiedUtc.Value,
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

    private static string FormatFieldErrors(IReadOnlyList<FieldError> errors)
    {
        return string.Join("; ", errors.Select(e => $"{e.FieldName}: {e.Message}"));
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
