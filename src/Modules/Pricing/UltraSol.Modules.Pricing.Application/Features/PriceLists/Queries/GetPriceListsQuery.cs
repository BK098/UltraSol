using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.PriceLists.Queries;

public sealed record GetPriceListsQuery(PagedFilter? Filter, PriceListType? Type = null, string? Currency = null) : IQuery<ApiResult<PaginatedResult<GetPriceListsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, string Currency, PriceListType Type);
}

public sealed class GetPriceListsValidator : AbstractValidator<GetPriceListsQuery>
{
    public GetPriceListsValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Currency).Must(value => value is null || value.Trim().Length == 3 && value.Trim().All(char.IsAsciiLetter)).WithMessage("Currency must contain three letters.");
    }
}

internal sealed class GetPriceListsHandler(IPricingReadStore reads) : IQueryHandler<GetPriceListsQuery, ApiResult<PaginatedResult<GetPriceListsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetPriceListsQuery.Response>>> Handle(GetPriceListsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}