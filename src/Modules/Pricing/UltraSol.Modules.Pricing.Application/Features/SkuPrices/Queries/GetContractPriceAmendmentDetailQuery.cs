using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetContractPriceAmendmentDetailQuery(Guid Id, Guid AmendmentId) : IQuery<ApiResult<GetContractPriceAmendmentDetailQuery.Response>>
{
    public sealed record Response(Guid Id, Guid ContractId, Guid PricePeriodId, PriceChangeKind Kind, ContractPriceAmendmentStatus Status, DateTimeOffset EffectiveFrom, IReadOnlyList<Tier> Tiers, string Reason, long ProposedRevision, Guid? ProposedBy, DateTimeOffset ProposedAt, Guid? DecidedBy, DateTimeOffset? DecidedAt);
    public sealed record Tier(int MinimumQuantity, decimal Amount);
}

public sealed class GetContractPriceAmendmentDetailValidator : AbstractValidator<GetContractPriceAmendmentDetailQuery>
{
    public GetContractPriceAmendmentDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AmendmentId).NotEmpty();
    }
}

internal sealed class GetContractPriceAmendmentDetailHandler(IPricingReadStore reads) : IQueryHandler<GetContractPriceAmendmentDetailQuery, ApiResult<GetContractPriceAmendmentDetailQuery.Response>>
{
    public async Task<ApiResult<GetContractPriceAmendmentDetailQuery.Response>> Handle(GetContractPriceAmendmentDetailQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}