using InventorySync.Core.Dtos;
using InventorySync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;

    public InventoryController(IInventoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetAll(
        [FromQuery] string? warehouseCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        return Ok(await _service.GetPagedAsync(warehouseCode, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> GetById(int id, CancellationToken ct = default)
    {
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<InventoryItemDto>> GetBySku(string sku, CancellationToken ct = default)
    {
        return Ok(await _service.GetBySkuAsync(sku, ct));
    }

    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> Create(CreateInventoryItemRequest request, CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> Update(int id, UpdateInventoryItemRequest request, CancellationToken ct = default)
    {
        return Ok(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
    }
}
