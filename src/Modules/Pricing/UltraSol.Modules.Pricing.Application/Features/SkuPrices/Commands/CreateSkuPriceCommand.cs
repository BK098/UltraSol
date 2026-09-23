using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;
using UltraSol.Modules.Pricing.Application.Integrations.Catalog;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Commands;

public sealed record CreateSkuPriceCommand(CreateSkuPriceDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreateSkuPriceValidator : AbstractValidator<CreateSkuPriceCommand>
{
    public CreateSkuPriceValidator()
    {
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.PriceListId).NotEmpty();
            RuleFor(x => x.Model!.SkuId).NotEmpty();
        });
    }
}

internal sealed class CreateSkuPriceHandler(IPriceListRepository lists, ISkuPriceRepository prices, IContractRepository contracts,
    ICatalogSkuClient catalog, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<CreateSkuPriceCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(CreateSkuPriceCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model!;
        var lookup = await catalog.ExistsAsync(model.SkuId, cancellationToken);
        if (!lookup.IsSuccess)
        {
            return ApiResultBuilder.Error<object>(lookup.Message ?? "Catalog SKU lookup failed.", lookup.StatusCode, lookup.Errors);
        }
        if (!lookup.Data)
        {
            throw new EntityNotFoundException("SKU", model.SkuId);
        }
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var list = await lists.GetRequiredByIdAsync(model.PriceListId, ct);
            Contract? contract = null;
            if (list.Type == PriceListType.Contract)
            {
                var owner = await contracts.FindByPriceListAsync(list.Id, ct) ??
                    throw new DomainException("The Contract price list has no owning contract.", "ContractRequired");
                contract = await contracts.GetLockedRequiredAsync(owner.Id, false, ct);
            }
            var price = SkuPrice.Create(list, model.SkuId, contract);
            price.MarkCreated(null, PricingTime.Normalize(clock.GetUtcNow()));
            await prices.AddAsync(price, ct);
            return ApiResultBuilder.Success<object>(price.Id, statusCode: 201);
        }, cancellationToken);
    }
}