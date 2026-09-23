using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetContractPriceAmendmentsQuery(Guid Id, PagedFilter? Filter) : IQuery<ApiResult<PaginatedResult<GetContractPriceAmendmentsQuery.Response>>>
{
    public sealed record Response(Guid Id, Guid ContractId, Guid PricePeriodId, PriceChangeKind Kind, ContractPriceAmendmentStatus Status, DateTimeOffset EffectiveFrom, DateTimeOffset ProposedAt);
}

public sealed class GetContractPriceAmendmentsValidator : AbstractValidator<GetContractPriceAmendmentsQuery>
{
    public GetContractPriceAmendmentsValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetContractPriceAmendmentsHandler(IPricingReadStore reads) : IQueryHandler<GetContractPriceAmendmentsQuery, ApiResult<PaginatedResult<GetContractPriceAmendmentsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetContractPriceAmendmentsQuery.Response>>> Handle(GetContractPriceAmendmentsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}