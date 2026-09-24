using InventorySync.Api.Attributes;

using InventorySync.Core.Dtos;
using InventorySync.Core.Enums;
using InventorySync.Core.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace InventorySync.Api.Controllers;

/// <summary>
/// CRUD operations for purchase orders.
/// </summary>
[ApiController]
[Route("api/purchaseorders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrdersController(IPurchaseOrderService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns a paged, filtered and sorted list of purchase orders.
    /// </summary>
    /// <param name="search">Filters orders whose PO number contains the given text.</param>
    /// <param name="vendorCode">Filters orders for the given vendor.</param>
    /// <param name="status">Filters orders by status.</param>
    /// <param name="orderDateFrom">Filters orders placed on or after the given UTC instant.</param>
    /// <param name="orderDateTo">Filters orders placed on or before the given UTC instant.</param>
    /// <param name="sort">Sort column: poNumber, vendorCode, status, orderDateUtc, expectedDateUtc, totalAmount. Prefix with '-' for descending.</param>
    /// <param name="includeLines">When true, loads each order's lines in the same query instead of forcing consumers into N+1 detail calls.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Page size (max 200).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [GenerateETag]
    [OutputCache(Duration = 30, VaryByQueryKeys = new[]
    {
        "search", "vendorCode", "status", "orderDateFrom", "orderDateTo", "sort", "includeLines", "page", "pageSize",
    })]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? vendorCode,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] DateTime? orderDateFrom,
        [FromQuery] DateTime? orderDateTo,
        [FromQuery] string sort = "poNumber",
        [FromQuery] bool includeLines = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new PurchaseOrderQuery
        {
            Search = search,
            VendorCode = vendorCode,
            Status = status,
            OrderDateFrom = orderDateFrom,
            OrderDateTo = orderDateTo,
            Sort = sort,
            IncludeLines = includeLines,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 200),
        };

        return Ok(await _service.GetPagedAsync(query, ct));
    }

    /// <summary>
    /// Returns a single purchase order including its lines.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(int id, CancellationToken ct = default)
    {
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// Creates a new purchase order in Draft status.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDto>> Create(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Submits a Draft purchase order.
    /// </summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(int id, CancellationToken ct = default)
    {
        return Ok(await _service.SubmitAsync(id, ct));
    }

    /// <summary>
    /// Records receipt of stock against a purchase order line.
    /// </summary>
    [HttpPost("{id:int}/lines/{lineId:int}/receive")]
    public async Task<ActionResult<PurchaseOrderDto>> ReceiveLine(int id, int lineId, ReceiveLineRequest request, CancellationToken ct = default)
    {
        return Ok(await _service.ReceiveLineAsync(id, lineId, request, ct));
    }

    /// <summary>
    /// Cancels a Draft or Submitted purchase order.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(int id, CancellationToken ct = default)
    {
        return Ok(await _service.CancelAsync(id, ct));
    }
}
