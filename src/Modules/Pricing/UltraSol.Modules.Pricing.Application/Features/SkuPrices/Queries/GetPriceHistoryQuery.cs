using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetPriceHistoryQuery(Guid Id, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetPriceHistoryQuery.Response>>>
{
    public sealed record Response(long Revision, PriceChangeKind Kind, Guid? ActorId, DateTimeOffset OccurredAt, IReadOnlyList<Period> Before, IReadOnlyList<Period> After, Guid? ContractPriceAmendmentId);
    public sealed record Period(Guid Id, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, IReadOnlyList<Tier> Tiers, Guid? ContractPriceAmendmentId);
    public sealed record Tier(int MinimumQuantity, decimal Amount);
}

public sealed class GetPriceHistoryValidator : AbstractValidator<GetPriceHistoryQuery>
{
    public GetPriceHistoryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetPriceHistoryHandler(IPricingReadStore reads) : IQueryHandler<GetPriceHistoryQuery, ApiResult<PaginatedResult<GetPriceHistoryQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetPriceHistoryQuery.Response>>> Handle(GetPriceHistoryQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}