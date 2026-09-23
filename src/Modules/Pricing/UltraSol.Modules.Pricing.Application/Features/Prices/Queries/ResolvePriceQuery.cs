using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.Prices.Models;
using UltraSol.Modules.Pricing.Application.Features.Prices.Services;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.Prices.Queries;

public sealed record ResolvePriceDto(Guid SkuId, int Quantity, string Currency, PriceListType Channel, DateTimeOffset? At = null,
    Guid? PriceListId = null, Guid? CustomerId = null, Guid? TransactionId = null, Guid? ContractId = null, Guid? NegotiatedPriceId = null);

public sealed record ResolvePriceQuery(ResolvePriceDto? Model) : IQuery<ApiResult<ResolvePriceQuery.Response>>
{
    public sealed record Response(decimal UnitPrice, string Currency, Guid SkuId, int Quantity, DateTimeOffset At, Guid PriceListId,
        Guid? SkuPriceId, Guid? PricePeriodId, Guid? ContractPriceAmendmentId, Guid? NegotiatedPriceId, Guid? ContractId);
}

public sealed class ResolvePriceValidator : AbstractValidator<ResolvePriceQuery>
{
    public ResolvePriceValidator()
    {
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.SkuId).NotEmpty();
            RuleFor(x => x.Model!.Quantity).GreaterThan(0);
            RuleFor(x => x.Model!.Currency).Must(value => value is not null && value.Trim().Length == 3 && value.Trim().All(char.IsAsciiLetter))
                .WithMessage("Currency must contain three letters.");
            RuleFor(x => x.Model!.Channel).IsInEnum();
            RuleFor(x => x.Model!.At).Must(value => value is null || value != default(DateTimeOffset));
            RuleFor(x => x.Model!.PriceListId).NotEqual(Guid.Empty);
            RuleFor(x => x.Model!.CustomerId).NotEqual(Guid.Empty);
            RuleFor(x => x.Model!.TransactionId).NotEqual(Guid.Empty);
            RuleFor(x => x.Model!.ContractId).NotEqual(Guid.Empty);
            RuleFor(x => x.Model!.NegotiatedPriceId).NotEqual(Guid.Empty);
            When(x => x.Model!.Channel == PriceListType.Contract, () =>
            {
                RuleFor(x => x.Model!.ContractId).NotNull();
                RuleFor(x => x.Model!.CustomerId).NotNull();
                RuleFor(x => x.Model!.PriceListId).Null().WithMessage("Contract pricing uses the contract's price list.");
            });
            When(x => x.Model!.Channel != PriceListType.Contract, () =>
            {
                RuleFor(x => x.Model!.PriceListId).NotNull();
                RuleFor(x => x.Model!.ContractId).Null();
            });
            When(x => x.Model!.NegotiatedPriceId is not null, () =>
            {
                RuleFor(x => x.Model!.CustomerId).NotNull();
                RuleFor(x => x.Model!.TransactionId).NotNull();
                RuleFor(x => x.Model!.Channel).NotEqual(PriceListType.Retail);
            });
        });
    }
}

internal sealed class ResolvePriceHandler(IPriceListRepository priceLists, ISkuPriceRepository skuPrices,
    IContractRepository contracts, INegotiatedPriceRepository negotiatedPrices, TimeProvider clock) : IQueryHandler<ResolvePriceQuery, ApiResult<ResolvePriceQuery.Response>>
{
    public async Task<ApiResult<ResolvePriceQuery.Response>> Handle(ResolvePriceQuery request, CancellationToken ct)
    {
        var model = request.Model!;
        try
        {
            Contract? contract = null;
            if (model.Channel == PriceListType.Contract)
            {
                contract = await contracts.GetRequiredByIdAsync(model.ContractId!.Value, ct);
            }
            var listId = contract?.PriceListId ?? model.PriceListId!.Value;
            var list = await priceLists.GetRequiredByIdAsync(listId, ct);
            var negotiated = model.NegotiatedPriceId is { } negotiatedId
                ? await negotiatedPrices.GetRequiredByIdAsync(negotiatedId, ct)
                : null;
            var skuPrice = negotiated is null
                ? await skuPrices.SingleOrDefaultAsync(row => row.PriceListId == listId && row.SkuId == model.SkuId, ct)
                : null;
            var resolved = PriceResolver.Resolve(new PriceResolutionRequest(model.SkuId, model.Quantity, Currency.Create(model.Currency),
                model.Channel, PricingTime.Normalize(model.At ?? clock.GetUtcNow()), model.CustomerId, model.TransactionId, model.ContractId, model.NegotiatedPriceId),
                list, skuPrice, negotiated, contract);
            return ApiResultBuilder.Success(new ResolvePriceQuery.Response(resolved.UnitPrice, resolved.Currency.Code, resolved.SkuId,
                resolved.Quantity, resolved.At, resolved.PriceListId, resolved.SkuPriceId, resolved.PricePeriodId,
                resolved.ContractPriceAmendmentId, resolved.NegotiatedPriceId, resolved.ContractId));
        }
        catch (DomainException exception)
        {
            return ApiResultBuilder.Error<ResolvePriceQuery.Response>(exception.Message, exception is EntityNotFoundException ? 404 : 409,
                new Dictionary<string, string[]> { ["Code"] = [exception.Code ?? "PricingError"] });
        }
    }
}