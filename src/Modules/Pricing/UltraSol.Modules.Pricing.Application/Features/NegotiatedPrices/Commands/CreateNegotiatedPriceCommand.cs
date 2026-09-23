using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Models;
using UltraSol.Modules.Pricing.Application.Integrations.Catalog;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.NegotiatedPrices.Commands;

public sealed record CreateNegotiatedPriceCommand(CreateNegotiatedPriceDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreateNegotiatedPriceValidator : AbstractValidator<CreateNegotiatedPriceCommand>
{
    public CreateNegotiatedPriceValidator(TimeProvider clock)
    {
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.CustomerId).NotEmpty();
            RuleFor(x => x.Model!.SkuId).NotEmpty();
            RuleFor(x => x.Model!.TransactionId).NotEmpty();
            RuleFor(x => x.Model!.Quantity).GreaterThan(0);
            RuleFor(x => x.Model!.Amount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Model!.Currency).Must(PricingInput.ValidCurrency).WithMessage("Currency must contain three ASCII letters.");
            RuleFor(x => x.Model!.Channel).Must(value => value is PriceListType.Wholesale or PriceListType.Contract)
                .WithMessage("Only Wholesale and Contract support negotiated pricing.");
            RuleFor(x => x.Model!.ContractId).NotNull().NotEqual(Guid.Empty).When(x => x.Model!.Channel == PriceListType.Contract);
            RuleFor(x => x.Model!.ContractId).Null().When(x => x.Model!.Channel == PriceListType.Wholesale);
            RuleFor(x => x.Model!.Reason).NotEmpty();
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty();
            RuleFor(x => x.Model!.EffectiveTo).NotEmpty().Must(value => PricingTime.Normalize(value) > PricingTime.Normalize(clock.GetUtcNow()))
                .WithMessage("EffectiveTo must be in the future.");
            RuleFor(x => x.Model!).Must(value => PricingTime.Normalize(value.EffectiveTo) > PricingTime.Normalize(value.EffectiveFrom))
                .WithMessage("EffectiveTo must be after EffectiveFrom.");
        });
    }
}

internal sealed class CreateNegotiatedPriceHandler(INegotiatedPriceRepository prices, IContractRepository contracts,
    ICatalogSkuClient catalog, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<CreateNegotiatedPriceCommand, ApiResult<object>>
{
    public async Task<ApiResult<object>> Handle(CreateNegotiatedPriceCommand request, CancellationToken cancellationToken)
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
            var currency = Currency.Create(model.Currency!);
            if (model.ContractId is { } id)
            {
                var contract = await contracts.GetLockedRequiredAsync(id, false, ct);
                if (contract.CustomerId != model.CustomerId || contract.Currency != currency)
                {
                    throw new DomainException("The customer and currency must match the contract.", "NegotiatedContractMismatch");
                }
            }
            var price = NegotiatedPrice.Create(model.CustomerId, model.SkuId, model.TransactionId, model.Quantity, model.Amount,
                currency, model.Channel, model.ContractId, model.Reason!,
                EffectivePeriod.Create(PricingTime.Normalize(model.EffectiveFrom), PricingTime.Normalize(model.EffectiveTo)),
                null, PricingTime.Normalize(clock.GetUtcNow()));
            await prices.AddAsync(price, ct);
            return ApiResultBuilder.Success<object>(price.Id, statusCode: 201);
        }, cancellationToken);
    }
}