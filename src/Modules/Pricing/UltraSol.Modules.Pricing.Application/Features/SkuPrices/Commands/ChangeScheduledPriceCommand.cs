using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ChangeScheduledPriceCommand(Guid Id, Guid PeriodId, PriceTiersDto? Model) : ICommand<ApiResult<object>>;

public sealed class ChangeScheduledPriceValidator : AbstractValidator<ChangeScheduledPriceCommand>
{
    public ChangeScheduledPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PeriodId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
        });
    }
}

internal sealed class ChangeScheduledPriceHandler(SkuPriceWriter writer) : ICommandHandler<ChangeScheduledPriceCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangeScheduledPriceCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            price.ChangeScheduledPrice(request.PeriodId, PricingInput.Tiers(request.Model!.Tiers!), actor, now, contract);
            return request.PeriodId;
        }, cancellationToken);
}