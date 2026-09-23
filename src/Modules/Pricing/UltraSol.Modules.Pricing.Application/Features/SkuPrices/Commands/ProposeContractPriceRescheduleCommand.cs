using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ProposeContractPriceRescheduleCommand(Guid Id, ProposePriceRescheduleDto? Model) : ICommand<ApiResult<object>>;

public sealed class ProposeContractPriceRescheduleValidator : AbstractValidator<ProposeContractPriceRescheduleCommand>
{
    public ProposeContractPriceRescheduleValidator(TimeProvider clock)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Reason).NotEmpty();
            RuleFor(x => x.Model!.PeriodId).NotEmpty();
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty().Must(value => PricingTime.Normalize(value) >= PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveFrom cannot be in the past.");
        });
    }
}

internal sealed class ProposeContractPriceRescheduleHandler(SkuPriceWriter writer) : ICommandHandler<ProposeContractPriceRescheduleCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ProposeContractPriceRescheduleCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
            price.ProposeContractPriceReschedule(PricingInput.RequireContract(contract), request.Model!.PeriodId, PricingTime.Normalize(request.Model.EffectiveFrom), request.Model.Reason!, actor, now), cancellationToken, statusCode: 201);
}