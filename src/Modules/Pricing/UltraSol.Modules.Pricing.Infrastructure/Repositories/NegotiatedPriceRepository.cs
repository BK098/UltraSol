using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Repositories;

public sealed class NegotiatedPriceRepository(PricingDbContext context) : Repository<NegotiatedPrice>(context), INegotiatedPriceRepository;