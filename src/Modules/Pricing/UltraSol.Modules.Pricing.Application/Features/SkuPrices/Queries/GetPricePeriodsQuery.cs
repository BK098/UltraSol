using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetPricePeriodsQuery(Guid Id, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetPricePeriodsQuery.Response>>>
{
    public sealed record Response(Guid Id, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, IReadOnlyList<Tier> Tiers, Guid? ContractPriceAmendmentId);
    public sealed record Tier(int MinimumQuantity, decimal Amount);
}

public sealed class GetPricePeriodsValidator : AbstractValidator<GetPricePeriodsQuery>
{
    public GetPricePeriodsValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetPricePeriodsHandler(IPricingReadStore reads) : IQueryHandler<GetPricePeriodsQuery, ApiResult<PaginatedResult<GetPricePeriodsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetPricePeriodsQuery.Response>>> Handle(GetPricePeriodsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}