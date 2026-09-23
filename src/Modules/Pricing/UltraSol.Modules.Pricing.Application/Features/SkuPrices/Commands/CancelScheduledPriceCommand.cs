using FluentValidation;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record CancelScheduledPriceCommand(Guid Id, Guid PeriodId) : ICommand<ApiResult<object>>;

public sealed class CancelScheduledPriceValidator : AbstractValidator<CancelScheduledPriceCommand>
{
    public CancelScheduledPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PeriodId).NotEmpty();
    }
}

internal sealed class CancelScheduledPriceHandler(SkuPriceWriter writer) : ICommandHandler<CancelScheduledPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CancelScheduledPriceCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            price.CancelScheduledPrice(request.PeriodId, actor, now, contract);
            return request.PeriodId;
        }, cancellationToken);
}