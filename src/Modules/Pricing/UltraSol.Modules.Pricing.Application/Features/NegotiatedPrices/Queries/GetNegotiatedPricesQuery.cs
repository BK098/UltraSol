using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Queries;

public sealed record GetNegotiatedPricesQuery(PagedFilter? Filter, Guid? CustomerId = null, Guid? SkuId = null, Guid? TransactionId = null, PriceListType? Channel = null, NegotiatedPriceStatus? Status = null) : IQuery<ApiResult<PaginatedResult<GetNegotiatedPricesQuery.Response>>>
{
    public sealed record Response(Guid Id, Guid CustomerId, Guid SkuId, Guid TransactionId, int Quantity, decimal Amount, string Currency, PriceListType Channel, NegotiatedPriceStatus Status, Guid? ContractId);
}

public sealed class GetNegotiatedPricesValidator : AbstractValidator<GetNegotiatedPricesQuery>
{
    public GetNegotiatedPricesValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty);
        RuleFor(x => x.SkuId).NotEqual(Guid.Empty);
        RuleFor(x => x.TransactionId).NotEqual(Guid.Empty);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class GetNegotiatedPricesHandler(IPricingReadStore reads) : IQueryHandler<GetNegotiatedPricesQuery, ApiResult<PaginatedResult<GetNegotiatedPricesQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetNegotiatedPricesQuery.Response>>> Handle(GetNegotiatedPricesQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}