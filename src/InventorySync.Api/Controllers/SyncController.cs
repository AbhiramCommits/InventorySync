using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Controllers;

/// <summary>
/// Triggers and inspects ERP synchronisation runs.
/// </summary>
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _service;

    public SyncController(ISyncService service)
    {
        _service = service;
    }

    /// <summary>
    /// Triggers a full inventory synchronisation from the ERP system and returns the resulting sync run.
    /// </summary>
    [HttpPost("inventory")]
    public async Task<ActionResult<SyncRunDto>> SyncInventory([FromQuery] string? triggeredBy, CancellationToken ct = default)
    {
        return Ok(await _service.SyncInventoryAsync(triggeredBy ?? "api", ct));
    }

    /// <summary>
    /// Triggers a full purchase order synchronisation from the ERP system and returns the resulting sync run.
    /// </summary>
    [HttpPost("purchase-orders")]
    public async Task<ActionResult<SyncRunDto>> SyncPurchaseOrders([FromQuery] string? triggeredBy, CancellationToken ct = default)
    {
        return Ok(await _service.SyncPurchaseOrdersAsync(triggeredBy ?? "api", ct));
    }

    /// <summary>
    /// Retries the records that failed in the given run and creates a new child sync run.
    /// </summary>
    [HttpPost("runs/{id:int}/retry")]
    public async Task<ActionResult<SyncRunDto>> RetryFailedRecords(int id, CancellationToken ct = default)
    {
        return Ok(await _service.RetryFailedRecordsAsync(id, ct));
    }

    /// <summary>
    /// Returns a paged list of sync runs, optionally filtered by entity type and status.
    /// </summary>
    [HttpGet("runs")]
    public async Task<ActionResult<PagedResult<SyncRunDto>>> GetRuns(
        [FromQuery] SyncEntityType? entityType,
        [FromQuery] SyncRunStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetRunsAsync(entityType, status, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct));
    }

    /// <summary>
    /// Returns a single sync run.
    /// </summary>
    [HttpGet("runs/{id:int}")]
    public async Task<ActionResult<SyncRunDto>> GetRun(int id, CancellationToken ct = default)
    {
        return Ok(await _service.GetRunByIdAsync(id, ct));
    }

    /// <summary>
    /// Returns the audit entries for a sync run, optionally filtered by action.
    /// </summary>
    [HttpGet("runs/{id:int}/audit")]
    public async Task<ActionResult<PagedResult<SyncAuditEntryDto>>> GetAuditEntries(
        int id,
        [FromQuery] SyncAuditAction? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetAuditEntriesAsync(id, action, Math.Max(1, page), Math.Clamp(pageSize, 1, 500), ct));
    }
}
