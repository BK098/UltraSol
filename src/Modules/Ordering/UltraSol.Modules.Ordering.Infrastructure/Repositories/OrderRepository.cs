using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Ordering.Infrastructure.Repositories;

public sealed class OrderRepository(OrderingDbContext context) : Repository<Order>(context), IOrderRepository;