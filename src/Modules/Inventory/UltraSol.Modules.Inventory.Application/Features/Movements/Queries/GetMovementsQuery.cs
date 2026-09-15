using FluentValidation;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Inventory.Application.Features.Movements.Queries;

public sealed record GetMovementsQuery(PagedFilter? Filter, Guid? ProductItemId, string? MovementType, Guid? ReferenceId,
    string? ReferenceType, DateTimeOffset? From, DateTimeOffset? To) : IQuery<ApiResult<PaginatedResult<GetMovementsQuery.Response>>>
{
    public sealed record Response(
        Guid Id,
        Guid WarehouseId,
        Guid ProductItemId,
        string MovementType,
        int QuantityDelta,
        string ReferenceType,
        Guid ReferenceId,
        string? Note,
        string? ActorId,
        DateTimeOffset OccurredAt);
}

public sealed class GetMovementsQueryValidator : AbstractValidator<GetMovementsQuery>
{
    public GetMovementsQueryValidator()
    {
        InventoryQueryPaging.AddRules(this, query => query.Filter);
        RuleFor(x => x.ProductItemId).NotEqual(Guid.Empty).When(x => x.ProductItemId.HasValue);
        RuleFor(x => x.ReferenceId).NotEqual(Guid.Empty).When(x => x.ReferenceId.HasValue);
        RuleFor(x => x.ReferenceType).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100)
            .When(x => x.ReferenceType is not null);
        RuleFor(x => x.MovementType).Must(value => Enum.TryParse<MovementType>(value, true, out var parsed) && Enum.IsDefined(parsed))
            .When(x => !string.IsNullOrWhiteSpace(x.MovementType));
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From!.Value).When(x => x.From.HasValue && x.To.HasValue);
    }
}

internal sealed class GetMovementsQueryHandler(IInventoryReadStore reads) : IQueryHandler<GetMovementsQuery, ApiResult<PaginatedResult<GetMovementsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetMovementsQuery.Response>>> Handle(GetMovementsQuery request, CancellationToken ct)
    {
        var result = await reads.MovementsAsync(InventoryQueryPaging.Create(request.Filter!), request.ProductItemId,
            request.MovementType, request.ReferenceId, request.ReferenceType, request.From, request.To, ct);
        return ApiResultBuilder.Success(result);
    }
}
