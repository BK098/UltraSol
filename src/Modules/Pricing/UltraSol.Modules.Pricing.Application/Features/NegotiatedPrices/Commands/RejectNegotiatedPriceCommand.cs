using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;

public sealed record RejectNegotiatedPriceCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class RejectNegotiatedPriceValidator : AbstractValidator<RejectNegotiatedPriceCommand>
{
    public RejectNegotiatedPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class RejectNegotiatedPriceHandler(INegotiatedPriceRepository prices, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<RejectNegotiatedPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RejectNegotiatedPriceCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var price = await prices.GetTrackedRequiredAsync(request.Id, ct);
            price.Reject(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(price.Id);
        }, cancellationToken);
}