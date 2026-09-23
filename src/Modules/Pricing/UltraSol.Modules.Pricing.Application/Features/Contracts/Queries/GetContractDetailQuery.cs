using FluentValidation;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.Contracts.Queries;

public sealed record GetContractDetailQuery(Guid Id) : IQuery<ApiResult<GetContractDetailQuery.Response>>
{
    public sealed record Response(Guid Id, Guid CustomerId, Guid PriceListId, string Currency, ContractStatus Status, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string CommercialTerms, Guid? ActivatedBy, DateTimeOffset? ActivatedAt, Guid? TerminatedBy, DateTimeOffset? TerminatedAt);
}

public sealed class GetContractDetailValidator : AbstractValidator<GetContractDetailQuery>
{
    public GetContractDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class GetContractDetailHandler(IPricingReadStore reads) : IQueryHandler<GetContractDetailQuery, ApiResult<GetContractDetailQuery.Response>>
{
    public async Task<ApiResult<GetContractDetailQuery.Response>> Handle(GetContractDetailQuery request, CancellationToken ct) =>
        ApiResultBuilder.Success(await reads.QueryAsync(request, ct));
}