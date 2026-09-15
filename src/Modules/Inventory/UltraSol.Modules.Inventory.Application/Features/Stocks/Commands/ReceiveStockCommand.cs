using FluentValidation;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Inventory.Application.Features.Stocks.Commands;

public sealed record ReceiveStockCommand(ReceiveStockRequest? Model, string? ActorId) : ICommand<ApiResult<ReceiptResult>>;

public sealed class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockCommandValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!).SetValidator(new ReceiveStockRequestValidator()).When(x => x.Model is not null);
    }
}

internal sealed class ReceiveStockCommandHandler(IInventoryModule module) : ICommandHandler<ReceiveStockCommand, ApiResult<ReceiptResult>>
{
    public async Task<ApiResult<ReceiptResult>> Handle(ReceiveStockCommand request, CancellationToken ct)
    {
        var result = await module.ReceiveAsync(request.Model!, request.ActorId, ct);
        return ApiResultBuilder.Success(result, statusCode: 201);
    }
}
