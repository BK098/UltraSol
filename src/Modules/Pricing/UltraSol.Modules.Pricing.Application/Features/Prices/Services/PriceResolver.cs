using UltraSol.Modules.Pricing.Application.Features.Prices.Models;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.Prices.Services;

public static class PriceResolver
{
    public static ResolvedPrice Resolve(PriceResolutionRequest request, PriceList priceList, SkuPrice? skuPrice, NegotiatedPrice? negotiatedPrice = null, Contract? contract = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(priceList);
        ValidateRequest(request);
        ValidatePriceList(request, priceList);
        ValidateContract(request, priceList, contract);
        ValidateSkuPrice(request, priceList, skuPrice);
        if (request.NegotiatedPriceId is not null)
        {
            return ResolveNegotiated(request, priceList, negotiatedPrice);
        }
        var configured = skuPrice?.Resolve(request.Quantity, request.At);
        if (configured is null)
        {
            throw new DomainException("No price applies to the requested SKU, quantity and time.", "NoApplicablePrice");
        }
        return new ResolvedPrice(configured.Amount, request.Currency, request.SkuId, request.Quantity, request.At, priceList.Id, skuPrice!.Id, configured.PricePeriodId, configured.ContractPriceAmendmentId, null, request.ContractId);
    }

    private static ResolvedPrice ResolveNegotiated(PriceResolutionRequest request, PriceList priceList, NegotiatedPrice? negotiatedPrice)
    {
        if (negotiatedPrice is null || negotiatedPrice.Id != request.NegotiatedPriceId)
        {
            throw new DomainException("The requested negotiated price was not supplied.", "NegotiatedPriceMismatch");
        }
        if (priceList.Type == PriceListType.Retail)
        {
            throw new DomainException("Retail prices cannot be negotiated.", "InvalidNegotiatedChannel");
        }
        if (request.CustomerId is null || request.TransactionId is null)
        {
            throw new DomainException("Negotiated prices require customer and transaction IDs.", "NegotiatedPriceContextRequired");
        }
        negotiatedPrice.EnsureApplicable(new NegotiatedPriceContext(request.SkuId, request.CustomerId.Value, request.TransactionId.Value, request.Quantity, request.Currency, request.Channel, request.At, request.ContractId));
        return new ResolvedPrice(negotiatedPrice.Amount, request.Currency, request.SkuId, request.Quantity, request.At, priceList.Id, null, null, null, negotiatedPrice.Id, request.ContractId);
    }

    private static void ValidateRequest(PriceResolutionRequest request)
    {
        if (request.SkuId == Guid.Empty)
        {
            throw new DomainException("SKU ID is required.", "InvalidSkuId");
        }
        if (request.Quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.", "InvalidQuantity");
        }
        ArgumentNullException.ThrowIfNull(request.Currency);
        if (!Enum.IsDefined(request.Channel))
        {
            throw new DomainException("Unknown price channel.", "InvalidPriceListType");
        }
        if (request.At == default)
        {
            throw new DomainException("Resolution time is required.", "InvalidTimestamp");
        }
        ValidateOptionalId(request.CustomerId, "InvalidCustomerId");
        ValidateOptionalId(request.TransactionId, "InvalidTransactionId");
        ValidateOptionalId(request.ContractId, "InvalidContractId");
        ValidateOptionalId(request.NegotiatedPriceId, "InvalidNegotiatedPriceId");
    }

    private static void ValidatePriceList(PriceResolutionRequest request, PriceList priceList)
    {
        if (request.Currency != priceList.Currency)
        {
            throw new DomainException("Request currency does not match the selected price list.", "PriceListCurrencyMismatch");
        }
        if (request.Channel != priceList.Type)
        {
            throw new DomainException("Request channel does not match the selected price list.", "PriceListTypeMismatch");
        }
    }

    private static void ValidateContract(PriceResolutionRequest request, PriceList priceList, Contract? contract)
    {
        if (priceList.Type != PriceListType.Contract)
        {
            if (request.ContractId is not null)
            {
                throw new DomainException("Only contract requests can contain a contract ID.", "UnexpectedContractId");
            }
            return;
        }
        if (request.ContractId is null || contract is null || contract.Id != request.ContractId || contract.PriceListId != priceList.Id || contract.Currency != priceList.Currency)
        {
            throw new DomainException("Contract does not match the selected price list.", "ContractMismatch");
        }
        if (request.CustomerId != contract.CustomerId)
        {
            throw new DomainException("Customer does not match the contract.", "ContractCustomerMismatch");
        }
        if (!contract.IsEffectiveAt(request.At))
        {
            throw new DomainException("Contract is not effective at the requested time.", "ContractNotEffective");
        }
    }

    private static void ValidateSkuPrice(PriceResolutionRequest request, PriceList priceList, SkuPrice? skuPrice)
    {
        if (skuPrice is null)
        {
            return;
        }
        if (skuPrice.PriceListId != priceList.Id)
        {
            throw new DomainException("SKU price does not belong to the selected price list.", "PriceListMismatch");
        }
        if (skuPrice.SkuId != request.SkuId)
        {
            throw new DomainException("SKU price does not belong to the requested SKU.", "SkuPriceMismatch");
        }
        if (skuPrice.Currency != priceList.Currency || skuPrice.Type != priceList.Type || skuPrice.ContractId != request.ContractId)
        {
            throw new DomainException("SKU price does not match the requested pricing context.", "SkuPriceContextMismatch");
        }
    }

    private static void ValidateOptionalId(Guid? value, string code)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("Identifier cannot be empty.", code);
        }
    }
}