using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Ordering.Infrastructure.Repositories;

public sealed class ShoppingCartRepository(OrderingDbContext context) : Repository<ShoppingCart>(context), IShoppingCartRepository;