using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Pricing.Application.Features.Contracts.Queries;

public sealed record GetContractsQuery(PagedFilter? Filter, Guid? CustomerId = null, ContractStatus? Status = null) : IQuery<ApiResult<PaginatedResult<GetContractsQuery.Response>>>
{
    public sealed record Response(Guid Id, Guid CustomerId, Guid PriceListId, string Currency, ContractStatus Status, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
}

public sealed class GetContractsValidator : AbstractValidator<GetContractsQuery>
{
    public GetContractsValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter!.PageIndex).InclusiveBetween(1, int.MaxValue / PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.Filter!.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize).When(x => x.Filter is not null);
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty);
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class GetContractsHandler(IPricingReadStore reads) : IQueryHandler<GetContractsQuery, ApiResult<PaginatedResult<GetContractsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetContractsQuery.Response>>> Handle(GetContractsQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}