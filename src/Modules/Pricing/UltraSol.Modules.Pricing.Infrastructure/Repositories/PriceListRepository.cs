using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Repositories;

public sealed class PriceListRepository(PricingDbContext context) : Repository<PriceList>(context), IPriceListRepository;