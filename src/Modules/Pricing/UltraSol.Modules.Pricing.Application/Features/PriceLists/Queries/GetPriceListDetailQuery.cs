using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.PriceLists.Queries;

public sealed record GetPriceListDetailQuery(Guid Id) : IQuery<ApiResult<GetPriceListDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, string Currency, PriceListType Type);
}

public sealed class GetPriceListDetailValidator : AbstractValidator<GetPriceListDetailQuery>
{
    public GetPriceListDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetPriceListDetailHandler(IPricingReadStore reads) : IQueryHandler<GetPriceListDetailQuery, ApiResult<GetPriceListDetailQuery.Response>>
{
    public async Task<ApiResult<GetPriceListDetailQuery.Response>> Handle(GetPriceListDetailQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}