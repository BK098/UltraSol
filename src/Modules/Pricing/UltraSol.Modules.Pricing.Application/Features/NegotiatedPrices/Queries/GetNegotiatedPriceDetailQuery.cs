using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Queries;

public sealed record GetNegotiatedPriceDetailQuery(Guid Id) : IQuery<ApiResult<GetNegotiatedPriceDetailQuery.Response>>
{
    public sealed record Response(Guid Id, Guid CustomerId, Guid SkuId, Guid TransactionId, int Quantity, decimal Amount, string Currency, PriceListType Channel, NegotiatedPriceStatus Status, Guid? ContractId, string Reason, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, Guid? ProposedBy, DateTimeOffset ProposedAt, Guid? SubmittedBy, DateTimeOffset? SubmittedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, Guid? RejectedBy, DateTimeOffset? RejectedAt, Guid? RevokedBy, DateTimeOffset? RevokedAt);
}

public sealed class GetNegotiatedPriceDetailValidator : AbstractValidator<GetNegotiatedPriceDetailQuery>
{
    public GetNegotiatedPriceDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetNegotiatedPriceDetailHandler(IPricingReadStore reads) : IQueryHandler<GetNegotiatedPriceDetailQuery, ApiResult<GetNegotiatedPriceDetailQuery.Response>>
{
    public async Task<ApiResult<GetNegotiatedPriceDetailQuery.Response>> Handle(GetNegotiatedPriceDetailQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}