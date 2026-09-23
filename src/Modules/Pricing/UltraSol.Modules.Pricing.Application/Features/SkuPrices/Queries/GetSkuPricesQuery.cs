using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetSkuPricesQuery(PagedFilter? Filter, Guid? PriceListId = null, Guid? SkuId = null, Guid? ContractId = null) : IQuery<ApiResult<PaginatedResult<GetSkuPricesQuery.Response>>>
{
    public sealed record Response(Guid Id, Guid PriceListId, Guid SkuId, string Currency, PriceListType Type, Guid? ContractId);
}

public sealed class GetSkuPricesValidator : AbstractValidator<GetSkuPricesQuery>
{
    public GetSkuPricesValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.PriceListId).NotEqual(Guid.Empty);
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty);
        RuleFor(x => x.ContractId).NotEqual(Guid.Empty);
    }
}

internal sealed class GetSkuPricesHandler(IPricingReadStore reads) : IQueryHandler<GetSkuPricesQuery, ApiResult<PaginatedResult<GetSkuPricesQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetSkuPricesQuery.Response>>> Handle(GetSkuPricesQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}