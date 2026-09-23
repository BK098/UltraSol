using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ChangePriceImmediatelyCommand(Guid Id, PriceTiersDto? Model) : ICommand<ApiResult<object>>;

public sealed class ChangePriceImmediatelyValidator : AbstractValidator<ChangePriceImmediatelyCommand>
{
    public ChangePriceImmediatelyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Tiers).Must(PricingInput.ValidTiers).WithMessage("Tiers require unique positive quantities and nonnegative amounts.");
        });
    }
}

internal sealed class ChangePriceImmediatelyHandler(SkuPriceWriter writer) : ICommandHandler<ChangePriceImmediatelyCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ChangePriceImmediatelyCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            price.ChangePriceImmediately(PricingInput.Tiers(request.Model!.Tiers!), actor, now, contract);
            return price.Id;
        }, cancellationToken);
}