using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;

public sealed record ApproveNegotiatedPriceCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class ApproveNegotiatedPriceValidator : AbstractValidator<ApproveNegotiatedPriceCommand>
{
    public ApproveNegotiatedPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class ApproveNegotiatedPriceHandler(INegotiatedPriceRepository prices, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<ApproveNegotiatedPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ApproveNegotiatedPriceCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var price = await prices.GetTrackedRequiredAsync(request.Id, ct);
            price.Approve(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(price.Id);
        }, cancellationToken);
}