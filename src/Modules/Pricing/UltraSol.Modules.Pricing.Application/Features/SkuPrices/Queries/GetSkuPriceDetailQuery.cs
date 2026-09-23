using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Queries;

public sealed record GetSkuPriceDetailQuery(Guid Id) : IQuery<ApiResult<GetSkuPriceDetailQuery.Response>>
{
    public sealed record Response(Guid Id, Guid PriceListId, Guid SkuId, string Currency, PriceListType Type, Guid? ContractId, long Revision);
}

public sealed class GetSkuPriceDetailValidator : AbstractValidator<GetSkuPriceDetailQuery>
{
    public GetSkuPriceDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetSkuPriceDetailHandler(IPricingReadStore reads) : IQueryHandler<GetSkuPriceDetailQuery, ApiResult<GetSkuPriceDetailQuery.Response>>
{
    public async Task<ApiResult<GetSkuPriceDetailQuery.Response>> Handle(GetSkuPriceDetailQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}