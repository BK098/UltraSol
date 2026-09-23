using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;

public sealed record RevokeNegotiatedPriceCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class RevokeNegotiatedPriceValidator : AbstractValidator<RevokeNegotiatedPriceCommand>
{
    public RevokeNegotiatedPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class RevokeNegotiatedPriceHandler(INegotiatedPriceRepository prices, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<RevokeNegotiatedPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RevokeNegotiatedPriceCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var price = await prices.GetTrackedRequiredAsync(request.Id, ct);
            price.Revoke(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(price.Id);
        }, cancellationToken);
}