using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ProposeContractScheduledPriceChangeCommand(Guid Id, ProposeScheduledPriceChangeDto? Model) : ICommand<ApiResult<object>>;

public sealed class ProposeContractScheduledPriceChangeValidator : AbstractValidator<ProposeContractScheduledPriceChangeCommand>
{
    public ProposeContractScheduledPriceChangeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Reason).NotEmpty();
            RuleFor(x => x.Model!.PeriodId).NotEmpty();
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
        });
    }
}

internal sealed class ProposeContractScheduledPriceChangeHandler(SkuPriceWriter writer) : ICommandHandler<ProposeContractScheduledPriceChangeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ProposeContractScheduledPriceChangeCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
            price.ProposeContractScheduledPriceChange(PricingInput.RequireContract(contract), request.Model!.PeriodId, PricingInput.Tiers(request.Model.Tiers!), request.Model.Reason!, actor, now), cancellationToken, statusCode: 201);
}