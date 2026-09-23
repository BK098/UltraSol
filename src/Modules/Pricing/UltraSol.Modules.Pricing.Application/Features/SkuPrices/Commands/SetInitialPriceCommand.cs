using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record SetInitialPriceCommand(Guid Id, InitialPriceDto? Model) : ICommand<ApiResult<object>>;

public sealed class SetInitialPriceValidator : AbstractValidator<SetInitialPriceCommand>
{
    public SetInitialPriceValidator(TimeProvider clock)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
            RuleFor(x => x.Model!.EffectiveFrom).Must(value => value is null || PricingTime.Normalize(value.Value) >= PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveFrom cannot be in the past.");
        });
    }
}

internal sealed class SetInitialPriceHandler(SkuPriceWriter writer) : ICommandHandler<SetInitialPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(SetInitialPriceCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            return price.SetInitialPrice(request.Model!.EffectiveFrom is { } from ? PricingTime.Normalize(from) : now, PricingInput.Tiers(request.Model.Tiers!), actor, now, contract);
        }, cancellationToken, statusCode: 201);
}