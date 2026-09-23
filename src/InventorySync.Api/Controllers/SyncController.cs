using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _service;

    public SyncController(ISyncService service)
    {
        _service = service;
    }

    [HttpPost("inventory")]
    public async Task<ActionResult<SyncResultDto>> SyncInventory(
        [FromQuery] string? triggeredBy,
        CancellationToken ct = default)
    {
        var result = await _service.SyncInventoryAsync(triggeredBy ?? "api", ct);
        return result.Status == SyncRunStatus.Failed ? StatusCode(StatusCodes.Status500InternalServerError, result) : Ok(result);
    }

    [HttpPost("purchaseorders")]
    public async Task<ActionResult<SyncResultDto>> SyncPurchaseOrders(
        [FromQuery] string? triggeredBy,
        CancellationToken ct = default)
    {
        var result = await _service.SyncPurchaseOrdersAsync(triggeredBy ?? "api", ct);
        return result.Status == SyncRunStatus.Failed ? StatusCode(StatusCodes.Status500InternalServerError, result) : Ok(result);
    }

    [HttpGet("runs")]
    public async Task<ActionResult<PagedResult<SyncRunDto>>> GetRuns(
        [FromQuery] SyncEntityType? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return Ok(await _service.GetRunsAsync(entityType, page, pageSize, ct));
    }

    [HttpGet("runs/{id:int}/audits")]
    public async Task<ActionResult<IReadOnlyList<SyncAuditEntryDto>>> GetAuditEntries(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        return Ok(await _service.GetAuditEntriesAsync(id, page, pageSize, ct));
    }
}
