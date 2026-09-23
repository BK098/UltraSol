using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;

public sealed class SkuPriceWriter(ISkuPriceRepository prices, IContractRepository contracts, IPricingUnitOfWork unitOfWork, TimeProvider clock)
{
    public Task<ApiResult<object>> ExecuteAsync(Guid id, Func<SkuPrice, Contract?, Guid?, DateTimeOffset, Guid> change,
        CancellationToken cancellationToken, int statusCode = 200) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            Guid? actorId = null;
            var contractId = await prices.GetContractIdAsync(id, ct);
            var contract = contractId is { } value ? await contracts.GetLockedRequiredAsync(value, false, ct) : null;
            var price = await prices.GetTrackedRequiredAsync(id, ct);
            var now = PricingTime.Normalize(clock.GetUtcNow());
            var result = change(price, contract, actorId, now);
            return ApiResultBuilder.Success<object>(result, statusCode: statusCode);
        }, cancellationToken);
}