using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;

public sealed record SubmitNegotiatedPriceCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class SubmitNegotiatedPriceValidator : AbstractValidator<SubmitNegotiatedPriceCommand>
{
    public SubmitNegotiatedPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class SubmitNegotiatedPriceHandler(INegotiatedPriceRepository prices, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<SubmitNegotiatedPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(SubmitNegotiatedPriceCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var price = await prices.GetTrackedRequiredAsync(request.Id, ct);
            price.Submit(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(price.Id);
        }, cancellationToken);
}