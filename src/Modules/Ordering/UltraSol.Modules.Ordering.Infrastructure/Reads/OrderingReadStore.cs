using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Ordering.Infrastructure.Reads;

public sealed class OrderingReadStore(OrderingDbContext db) : IOrderingReadStore
{
    public async Task<OrderListResponse> ListAsync(string? ownerKey, PagedFilter filter, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (ownerKey is not null)
        {
            query = query.Where(order => order.OwnerKey == ownerKey);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(order => order.OrderNumber.Contains(search));
        }
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(order => order.PlacedAt).ThenByDescending(order => order.Id)
            .Skip(checked((filter.PageIndex - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(order => new OrderListItem(order.Id, order.OrderNumber, order.Status.ToString(), order.Currency, order.GrandTotal, order.PlacedAt))
            .ToArrayAsync(ct);
        return new(filter.PageIndex, filter.PageSize, count, items);
    }
}