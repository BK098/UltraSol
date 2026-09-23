using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ReschedulePriceChangeCommand(Guid Id, Guid PeriodId, ReschedulePriceDto? Model) : ICommand<ApiResult<object>>;

public sealed class ReschedulePriceChangeValidator : AbstractValidator<ReschedulePriceChangeCommand>
{
    public ReschedulePriceChangeValidator(TimeProvider clock)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PeriodId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty().Must(value => PricingTime.Normalize(value) > PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveFrom must be in the future.");
        });
    }
}

internal sealed class ReschedulePriceChangeHandler(SkuPriceWriter writer) : ICommandHandler<ReschedulePriceChangeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ReschedulePriceChangeCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            price.ReschedulePriceChange(request.PeriodId, PricingTime.Normalize(request.Model!.EffectiveFrom), actor, now, contract);
            return request.PeriodId;
        }, cancellationToken);
}