using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySync.Api.Controllers;

[ApiController]
[Route("api/purchaseorders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrdersController(IPurchaseOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> GetAll(
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return Ok(await _service.GetPagedAsync(status, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(int id, CancellationToken ct = default)
    {
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDto>> Create(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(int id, CancellationToken ct = default)
    {
        return Ok(await _service.SubmitAsync(id, ct));
    }

    [HttpPost("{id:int}/lines/{lineId:int}/receive")]
    public async Task<ActionResult<PurchaseOrderDto>> ReceiveLine(int id, int lineId, ReceiveLineRequest request, CancellationToken ct = default)
    {
        return Ok(await _service.ReceiveLineAsync(id, lineId, request, ct));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(int id, CancellationToken ct = default)
    {
        return Ok(await _service.CancelAsync(id, ct));
    }
}
