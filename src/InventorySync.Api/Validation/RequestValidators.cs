using FluentValidation;

using InventorySync.Core.Dtos;

namespace InventorySync.Api.Validation;

public class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.QuantityOnHand).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WarehouseCode).NotEmpty().MaximumLength(20);
    }
}

public class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.QuantityOnHand).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WarehouseCode).NotEmpty().MaximumLength(20);
    }
}

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.PoNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.VendorCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines).Must(lines => lines.All(l => l.QuantityOrdered > 0))
            .WithMessage("Line quantities must be greater than zero.");
        RuleFor(x => x).Must(x => x.ExpectedDateUtc >= x.OrderDateUtc)
            .WithMessage("ExpectedDateUtc cannot be earlier than OrderDateUtc.");
        RuleForEach(x => x.Lines).SetValidator(new CreatePurchaseOrderLineRequestValidator());
    }
}

public class CreatePurchaseOrderLineRequestValidator : AbstractValidator<CreatePurchaseOrderLineRequest>
{
    public CreatePurchaseOrderLineRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.QuantityOrdered).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class ReceiveLineRequestValidator : AbstractValidator<ReceiveLineRequest>
{
    public ReceiveLineRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
