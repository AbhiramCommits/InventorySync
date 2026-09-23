using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Enums;
using InventorySync.Core.Exceptions;
using InventorySync.Core.Interfaces;

namespace InventorySync.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;

    public PurchaseOrderService(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(
        PurchaseOrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var result = await _repository.GetPagedAsync(status, page, pageSize, ct);

        return new PagedResult<PurchaseOrderDto>
        {
            Items = result.Items.Select(DtoMapper.ToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, true, ct)
            ?? throw new EntityNotFoundException($"Purchase order with id {id} was not found.");

        return DtoMapper.ToDto(order);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PoNumber))
        {
            throw new InvalidOperationException("PoNumber is required.");
        }

        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("A purchase order must contain at least one line.");
        }

        if (request.Lines.Any(l => l.QuantityOrdered <= 0))
        {
            throw new InvalidOperationException("Line quantities must be greater than zero.");
        }

        if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.Sku)))
        {
            throw new InvalidOperationException("Line SKUs are required.");
        }

        if (request.ExpectedDateUtc < request.OrderDateUtc)
        {
            throw new InvalidOperationException("ExpectedDateUtc cannot be earlier than OrderDateUtc.");
        }

        if (await _repository.PoNumberExistsAsync(request.PoNumber.Trim(), ct))
        {
            throw new ConflictException($"A purchase order with number '{request.PoNumber}' already exists.");
        }

        var order = new PurchaseOrder
        {
            PoNumber = request.PoNumber.Trim(),
            VendorCode = request.VendorCode,
            Status = PurchaseOrderStatus.Draft,
            OrderDateUtc = request.OrderDateUtc,
            ExpectedDateUtc = request.ExpectedDateUtc,
            Lines = request.Lines.Select(l => new PurchaseOrderLine
            {
                Sku = l.Sku.Trim(),
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = 0,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };

        order.TotalAmount = Math.Round(order.Lines.Sum(l => l.QuantityOrdered * l.UnitPrice), 2);

        await _repository.AddAsync(order, ct);
        await _repository.SaveChangesAsync(ct);

        return DtoMapper.ToDto(order);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, true, ct)
            ?? throw new EntityNotFoundException($"Purchase order with id {id} was not found.");

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft orders can be submitted (current status: {order.Status}).");
        }

        order.Status = PurchaseOrderStatus.Submitted;
        await _repository.SaveChangesAsync(ct);

        return DtoMapper.ToDto(order);
    }

    public async Task<PurchaseOrderDto> ReceiveLineAsync(int id, int lineId, ReceiveLineRequest request, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, true, ct)
            ?? throw new EntityNotFoundException($"Purchase order with id {id} was not found.");

        if (order.Status is not (PurchaseOrderStatus.Submitted or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new InvalidOperationException($"Only Submitted or PartiallyReceived orders can receive stock (current status: {order.Status}).");
        }

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new EntityNotFoundException($"Line with id {lineId} was not found on purchase order {id}.");

        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var remaining = line.QuantityOrdered - line.QuantityReceived;
        if (request.Quantity > remaining)
        {
            throw new InvalidOperationException($"Cannot receive {request.Quantity} units; only {remaining} remain on line '{line.Sku}'.");
        }

        line.QuantityReceived += request.Quantity;
        order.Status = order.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;

        await _repository.SaveChangesAsync(ct);

        return DtoMapper.ToDto(order);
    }

    public async Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, true, ct)
            ?? throw new EntityNotFoundException($"Purchase order with id {id} was not found.");

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted))
        {
            throw new InvalidOperationException($"Only Draft or Submitted orders can be cancelled (current status: {order.Status}).");
        }

        order.Status = PurchaseOrderStatus.Cancelled;
        await _repository.SaveChangesAsync(ct);

        return DtoMapper.ToDto(order);
    }
}
