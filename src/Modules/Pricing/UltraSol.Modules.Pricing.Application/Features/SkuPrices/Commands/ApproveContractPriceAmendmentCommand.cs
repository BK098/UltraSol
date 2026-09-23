using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record ApproveContractPriceAmendmentCommand(Guid Id, Guid AmendmentId) : ICommand<ApiResult<object>>;

public sealed class ApproveContractPriceAmendmentValidator : AbstractValidator<ApproveContractPriceAmendmentCommand>
{
    public ApproveContractPriceAmendmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AmendmentId).NotEmpty();
    }
}

internal sealed class ApproveContractPriceAmendmentHandler(SkuPriceWriter writer) : ICommandHandler<ApproveContractPriceAmendmentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ApproveContractPriceAmendmentCommand request, CancellationToken cancellationToken) =>
        writer.ExecuteAsync(request.Id, (price, contract, actor, now) =>
        {
            price.ApproveContractPriceAmendment(PricingInput.RequireContract(contract), request.AmendmentId, actor, now);
            return request.AmendmentId;
        }, cancellationToken);
}