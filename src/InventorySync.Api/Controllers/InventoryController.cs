using InventorySync.Core.Dtos;
using InventorySync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Controllers;

/// <summary>
/// CRUD operations for inventory items.
/// </summary>
[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;

    public InventoryController(IInventoryService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns a paged, filtered and sorted list of inventory items.
    /// </summary>
    /// <param name="search">Filters items whose SKU contains the given text.</param>
    /// <param name="warehouseCode">Filters items in the given warehouse.</param>
    /// <param name="lastSyncedFrom">Filters items synced on or after the given UTC instant.</param>
    /// <param name="lastSyncedTo">Filters items synced on or before the given UTC instant.</param>
    /// <param name="sort">Sort column: sku, name, quantityOnHand, unitCost, warehouseCode, lastSyncedUtc. Prefix with '-' for descending.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Page size (max 200).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? warehouseCode,
        [FromQuery] DateTime? lastSyncedFrom,
        [FromQuery] DateTime? lastSyncedTo,
        [FromQuery] string sort = "sku",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new InventoryItemQuery
        {
            Search = search,
            WarehouseCode = warehouseCode,
            LastSyncedFrom = lastSyncedFrom,
            LastSyncedTo = lastSyncedTo,
            Sort = sort,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 200),
        };

        return Ok(await _service.GetPagedAsync(query, ct));
    }

    /// <summary>
    /// Returns a single inventory item by id.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> GetById(int id, CancellationToken ct = default)
    {
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// Returns a single inventory item by SKU.
    /// </summary>
    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<InventoryItemDto>> GetBySku(string sku, CancellationToken ct = default)
    {
        return Ok(await _service.GetBySkuAsync(sku, ct));
    }

    /// <summary>
    /// Creates a new inventory item.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> Create(CreateInventoryItemRequest request, CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing inventory item. Supply the current RowVersion for optimistic concurrency.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> Update(int id, UpdateInventoryItemRequest request, CancellationToken ct = default)
    {
        return Ok(await _service.UpdateAsync(id, request, ct));
    }

    /// <summary>
    /// Deletes an inventory item.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
