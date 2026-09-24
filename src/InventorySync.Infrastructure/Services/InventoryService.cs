using InventorySync.Core.Dtos;
using InventorySync.Core.Entities;
using InventorySync.Core.Exceptions;
using InventorySync.Core.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Infrastructure.Services;

/// <summary>
/// Default implementation of the inventory business operations.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IInventoryItemRepository _repository;

    /// <summary>
    /// Initializes a new instance of the InventoryService class.
    /// </summary>
    public InventoryService(IInventoryItemRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets a paged set of records.
    /// </summary>
    public async Task<PagedResult<InventoryItemDto>> GetPagedAsync(InventoryItemQuery query, CancellationToken ct = default)
    {
        var result = await _repository.GetPagedAsync(query, ct);

        return new PagedResult<InventoryItemDto>
        {
            Items = result.Items.Select(DtoMapper.ToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    /// <summary>
    /// Gets a record by id.
    /// </summary>
    public async Task<InventoryItemDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await _repository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Inventory item with id {id} was not found.");

        return DtoMapper.ToDto(item);
    }

    /// <summary>
    /// Gets an item by SKU.
    /// </summary>
    public async Task<InventoryItemDto> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var item = await _repository.GetBySkuAsync(sku, ct)
            ?? throw new EntityNotFoundException($"Inventory item with SKU '{sku}' was not found.");

        return DtoMapper.ToDto(item);
    }

    /// <summary>
    /// create async.
    /// </summary>
    public async Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new InvalidOperationException("Sku is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (await _repository.SkuExistsAsync(request.Sku.Trim(), ct))
        {
            throw new ConflictException($"An inventory item with SKU '{request.Sku}' already exists.");
        }

        var item = new InventoryItem
        {
            Sku = request.Sku.Trim(),
            Name = request.Name,
            Description = request.Description,
            QuantityOnHand = request.QuantityOnHand,
            UnitCost = request.UnitCost,
            WarehouseCode = request.WarehouseCode,
            LocallyModifiedUtc = DateTime.UtcNow,
        };

        await _repository.AddAsync(item, ct);
        await _repository.SaveChangesAsync(ct);

        return DtoMapper.ToDto(item);
    }

    /// <summary>
    /// Marks a record as updated.
    /// </summary>
    public async Task<InventoryItemDto> UpdateAsync(int id, UpdateInventoryItemRequest request, CancellationToken ct = default)
    {
        var item = await _repository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Inventory item with id {id} was not found.");

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            byte[] requested;
            try
            {
                requested = Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException)
            {
                throw new ConflictException("The supplied RowVersion is not valid Base64.");
            }

            _repository.SetOriginalRowVersion(item, requested);
        }

        item.Name = request.Name;
        item.Description = request.Description;
        item.QuantityOnHand = request.QuantityOnHand;
        item.UnitCost = request.UnitCost;
        item.WarehouseCode = request.WarehouseCode;
        item.LocallyModifiedUtc = DateTime.UtcNow;

        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The inventory item was modified concurrently. Re-fetch and retry.");
        }

        return DtoMapper.ToDto(item);
    }

    /// <summary>
    /// Marks a record as deleted.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await _repository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Inventory item with id {id} was not found.");

        _repository.Delete(item);
        await _repository.SaveChangesAsync(ct);
    }
}
