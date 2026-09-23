using UltraSol.Modules.Ordering.Application.Messaging;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.IntegrationEvents.Ordering;

namespace UltraSol.Modules.Ordering.Infrastructure.Messaging;

public sealed class OrderingOutbox(Mailbox<OrderingDbContext> mailbox, IEnumerable<ModuleMailbox> modules) : IOrderingOutbox
{
    public void Placed(Order order)
    {
        var buyer = order.Buyer;
        Add(new OrderPlacedV1(Guid.CreateVersion7(), order.Id, 1, order.PlacedAt, order.Id, order.OrderVersion,
            order.OrderNumber, buyer.BuyerType, buyer.IdentityUserId, buyer.CustomerId, buyer.BusinessAccountId,
            buyer.Name, buyer.Email, buyer.Phone, order.GrandTotal, order.Currency, order.PaymentTerm.Type,
            order.PaymentTerm.NetDays, order.ReservationExpiresAt, order.Status.ToString()));
    }

    public void Changed(Order order)
    {
        var history = order.History.Single(item => item.Version == order.OrderVersion);
        var buyer = order.Buyer;
        switch (order.Status)
        {
            case OrderStatus.Cancelled:
                Add(new OrderCancelledV1(Guid.CreateVersion7(), order.Id, 1, history.OccurredAt, order.Id, order.OrderVersion, history.Reason!));
                break;
            case OrderStatus.Rejected:
                Add(new OrderRejectedV1(Guid.CreateVersion7(), order.Id, 1, history.OccurredAt, order.Id, order.OrderVersion, history.Reason!));
                break;
            case OrderStatus.Completed:
                Add(new OrderCompletedV1(Guid.CreateVersion7(), order.Id, 1, order.CompletedAt!.Value, order.Id, order.OrderVersion,
                    buyer.BuyerType, buyer.IdentityUserId, buyer.CustomerId, buyer.BusinessAccountId, buyer.Name, buyer.Email, buyer.Phone));
                break;
        }
    }

    private void Add<T>(T message)
    {
        if (modules.Any(module => module.Subscriptions.Contains(typeof(T))))
        {
            mailbox.Add(message);
        }
    }
}
