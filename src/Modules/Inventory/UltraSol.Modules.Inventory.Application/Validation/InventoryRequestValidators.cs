using FluentValidation;
using FluentValidation.Results;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Domain.Adjustments;

namespace UltraSol.Modules.Inventory.Application.Validation;

public sealed class ReceiveStockRequestValidator : AbstractValidator<ReceiveStockRequest>
{
    public ReceiveStockRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ReferenceType).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100);
        RuleFor(x => x.ReferenceId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class ReserveStockRequestValidator : AbstractValidator<ReserveStockRequest>
{
    public ReserveStockRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Lines).Custom(InventoryRequestValidation.ValidateStockLines);
    }
}

public sealed class CreateAdjustmentRequestValidator : AbstractValidator<CreateAdjustmentRequest>
{
    public CreateAdjustmentRequestValidator()
    {
        RuleFor(x => x.Reason).Custom(InventoryRequestValidation.ValidateReason);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Lines).Custom(InventoryRequestValidation.ValidateAdjustmentLines);
    }
}

public sealed class UpdateAdjustmentRequestValidator : AbstractValidator<UpdateAdjustmentRequest>
{
    public UpdateAdjustmentRequestValidator()
    {
        RuleFor(x => x.ConcurrencyStamp).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(64);
        RuleFor(x => x.Reason).Custom(InventoryRequestValidation.ValidateReason);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Lines).Custom(InventoryRequestValidation.ValidateAdjustmentLines);
    }
}

internal static class InventoryRequestValidation
{
    internal static void ValidateStockLines(IReadOnlyList<StockLineRequest>? lines, ValidationContext<ReserveStockRequest> context)
    {
        if (!ValidateLines(lines, context))
        {
            return;
        }
        for (var index = 0; index < lines!.Count; index++)
        {
            var line = lines[index];
            if (line is null)
            {
                context.AddFailure($"Lines[{index}]", "Line is required.");
                continue;
            }
            if (line.ProductItemId == Guid.Empty)
            {
                context.AddFailure($"Lines[{index}].{nameof(StockLineRequest.ProductItemId)}", "ProductItemId is required.");
            }
            if (line.Quantity <= 0)
            {
                context.AddFailure($"Lines[{index}].{nameof(StockLineRequest.Quantity)}", "Quantity must be greater than zero.");
            }
        }
        AddDuplicateFailure(lines.Where(line => line is not null).Select(line => line.ProductItemId), context);
    }

    internal static void ValidateAdjustmentLines<T>(IReadOnlyList<AdjustmentLineRequest>? lines, ValidationContext<T> context)
    {
        if (!ValidateLines(lines, context))
        {
            return;
        }
        for (var index = 0; index < lines!.Count; index++)
        {
            var line = lines[index];
            if (line is null)
            {
                context.AddFailure($"Lines[{index}]", "Line is required.");
                continue;
            }
            if (line.ProductItemId == Guid.Empty)
            {
                context.AddFailure($"Lines[{index}].{nameof(AdjustmentLineRequest.ProductItemId)}", "ProductItemId is required.");
            }
            if (line.QuantityDelta == 0)
            {
                context.AddFailure($"Lines[{index}].{nameof(AdjustmentLineRequest.QuantityDelta)}", "QuantityDelta must not be zero.");
            }
        }
        AddDuplicateFailure(lines.Where(line => line is not null).Select(line => line.ProductItemId), context);
    }

    internal static void ValidateReason(string? reason, ValidationContext<CreateAdjustmentRequest> context) =>
        ValidateReason(reason, message => context.AddFailure(nameof(CreateAdjustmentRequest.Reason), message));

    internal static void ValidateReason(string? reason, ValidationContext<UpdateAdjustmentRequest> context) =>
        ValidateReason(reason, message => context.AddFailure(nameof(UpdateAdjustmentRequest.Reason), message));

    private static bool ValidateLines<TLine, TRequest>(IReadOnlyList<TLine>? lines, ValidationContext<TRequest> context)
    {
        if (lines is null)
        {
            context.AddFailure("Lines", "Lines are required.");
            return false;
        }
        if (lines.Count is < 1 or > 200)
        {
            context.AddFailure("Lines", "Lines must contain between 1 and 200 items.");
            return false;
        }
        return true;
    }

    private static void AddDuplicateFailure<TRequest>(IEnumerable<Guid> productItemIds, ValidationContext<TRequest> context)
    {
        if (productItemIds.GroupBy(id => id).Any(group => group.Count() > 1))
        {
            context.AddFailure("Lines", "Duplicate ProductItemId values are not allowed.");
        }
    }

    private static void ValidateReason(string? reason, Action<string> addFailure)
    {
        if (string.IsNullOrWhiteSpace(reason) ||
            !Enum.TryParse<AdjustmentReason>(reason, true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            addFailure("Reason must be a defined AdjustmentReason.");
        }
    }
}
