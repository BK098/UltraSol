using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Application.Features.Adjustments.Queries;

public sealed record GetAdjustmentsQuery(PagedFilter? Filter, string? Status) : IQuery<ApiResult<PaginatedResult<GetAdjustmentsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Reason, string? Note, string Status, DateTimeOffset CreatedAt);
}

public sealed class GetAdjustmentsQueryValidator : AbstractValidator<GetAdjustmentsQuery>
{
    public GetAdjustmentsQueryValidator()
    {
        InventoryQueryPaging.AddRules(this, query => query.Filter);
        RuleFor(x => x.Status).Must(status => Enum.TryParse<AdjustmentStatus>(status, true, out var parsed) && Enum.IsDefined(parsed))
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

internal sealed class GetAdjustmentsQueryHandler(IInventoryReadStore reads)
    : IQueryHandler<GetAdjustmentsQuery, ApiResult<PaginatedResult<GetAdjustmentsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetAdjustmentsQuery.Response>>> Handle(GetAdjustmentsQuery request, CancellationToken ct)
    {
        var result = await reads.AdjustmentsAsync(InventoryQueryPaging.Create(request.Filter!), request.Status, ct);
        return ApiResultBuilder.Success(result);
    }
}
