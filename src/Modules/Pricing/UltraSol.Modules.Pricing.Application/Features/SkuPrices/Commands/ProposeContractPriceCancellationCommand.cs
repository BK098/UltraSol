using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ProposeContractPriceCancellationCommand(Guid Id, ProposePriceCancellationDto? Model) : ICommand<ApiResult<object>>;

public sealed class ProposeContractPriceCancellationValidator : AbstractValidator<ProposeContractPriceCancellationCommand>
{
    public ProposeContractPriceCancellationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Reason).NotEmpty();
            RuleFor(x => x.Model!.PeriodId).NotEmpty();
        });
    }
}

internal sealed class ProposeContractPriceCancellationHandler(SkuPriceWriter writer) : ICommandHandler<ProposeContractPriceCancellationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ProposeContractPriceCancellationCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
            price.ProposeContractPriceCancellation(PricingInput.RequireContract(contract), request.Model!.PeriodId, request.Model.Reason!, actor, now), cancellationToken, statusCode: 201);
}