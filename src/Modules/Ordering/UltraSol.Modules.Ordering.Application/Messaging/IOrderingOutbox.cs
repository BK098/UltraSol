using UltraSol.Modules.Ordering.Domain.Orders;

namespace UltraSol.Modules.Ordering.Application.Messaging;

public interface IOrderingOutbox
{
    void Placed(Order order);
    void Changed(Order order);
}
