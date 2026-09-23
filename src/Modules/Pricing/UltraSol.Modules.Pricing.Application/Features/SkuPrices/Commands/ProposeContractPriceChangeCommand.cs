using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ProposeContractPriceChangeCommand(Guid Id, ProposePriceChangeDto? Model) : ICommand<ApiResult<object>>;

public sealed class ProposeContractPriceChangeValidator : AbstractValidator<ProposeContractPriceChangeCommand>
{
    public ProposeContractPriceChangeValidator(TimeProvider clock)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Reason).NotEmpty();
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty().Must(value => PricingTime.Normalize(value) >= PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveFrom cannot be in the past.");
        });
    }
}

internal sealed class ProposeContractPriceChangeHandler(SkuPriceWriter writer) : ICommandHandler<ProposeContractPriceChangeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ProposeContractPriceChangeCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
            price.ProposeContractPriceChange(PricingInput.RequireContract(contract), PricingTime.Normalize(request.Model!.EffectiveFrom), PricingInput.Tiers(request.Model.Tiers!), request.Model.Reason!, actor, now), cancellationToken, statusCode: 201);
}