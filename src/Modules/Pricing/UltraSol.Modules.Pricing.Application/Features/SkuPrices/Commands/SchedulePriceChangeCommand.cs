using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record SchedulePriceChangeCommand(Guid Id, ScheduledPriceDto? Model) : ICommand<ApiResult<object>>;

public sealed class SchedulePriceChangeValidator : AbstractValidator<SchedulePriceChangeCommand>
{
    public SchedulePriceChangeValidator(TimeProvider clock)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty().Must(value => PricingTime.Normalize(value) > PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveFrom must be in the future.");
        });
    }
}

internal sealed class SchedulePriceChangeHandler(SkuPriceWriter writer) : ICommandHandler<SchedulePriceChangeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(SchedulePriceChangeCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            return price.SchedulePriceChange(PricingTime.Normalize(request.Model!.EffectiveFrom), PricingInput.Tiers(request.Model.Tiers!), actor, now, contract);
        }, cancellationToken, statusCode: 201);
}