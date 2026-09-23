using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Application.Features.Orders;

public sealed record OrderListItem(Guid Id, string OrderNumber, string Status, string Currency, decimal GrandTotal, DateTimeOffset PlacedAt);
public sealed record OrderListResponse(int PageIndex, int PageSize, long TotalCount, OrderListItem[] Items);

public interface IOrderingReadStore
{
    Task<OrderListResponse> ListAsync(string? ownerKey, PagedFilter filter, CancellationToken ct);
}