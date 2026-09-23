using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record RejectContractPriceAmendmentCommand(Guid Id, Guid AmendmentId) : ICommand<ApiResult<object>>;

public sealed class RejectContractPriceAmendmentValidator : AbstractValidator<RejectContractPriceAmendmentCommand>
{
    public RejectContractPriceAmendmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AmendmentId).NotEmpty();
    }
}

internal sealed class RejectContractPriceAmendmentHandler(SkuPriceWriter writer) : ICommandHandler<RejectContractPriceAmendmentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RejectContractPriceAmendmentCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            PricingInput.RequireContract(contract);
            price.RejectContractPriceAmendment(request.AmendmentId, actor, now);
            return request.AmendmentId;
        }, cancellationToken);
}