using UltraSol.Modules.Pricing.Application.Features.Prices.Services;
using UltraSol.Modules.Pricing.Application.Features.Prices.Models;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PriceResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Currency Vnd = Currency.Create("VND");

    [Fact]
    public void Configured_price_returns_reproducible_all_units_snapshot()
    {
        var list = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var price = SkuPrice.Create(list, skuId);
        var periodId = price.SetInitialPrice(Now, [PriceTier.Create(1, 100), PriceTier.Create(10, 90)], Actor, Now);

        var result = PriceResolver.Resolve(Request(skuId, list, quantity: 12), list, price);

        Assert.Equal(90, result.UnitPrice);
        Assert.Equal(Vnd, result.Currency);
        Assert.Equal(skuId, result.SkuId);
        Assert.Equal(12, result.Quantity);
        Assert.Equal(Now, result.At);
        Assert.Equal(list.Id, result.PriceListId);
        Assert.Equal(price.Id, result.SkuPriceId);
        Assert.Equal(periodId, result.PricePeriodId);
        Assert.Null(result.ContractPriceAmendmentId);
        Assert.Null(result.NegotiatedPriceId);
        Assert.Null(result.ContractId);
    }

    [Fact]
    public void Configured_price_does_not_fallback_when_no_tier_applies()
    {
        var list = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var price = SkuPrice.Create(list, skuId);
        price.SetInitialPrice(Now, [PriceTier.Create(10, 90)], Actor, Now);

        var exception = Assert.Throws<DomainException>(() => PriceResolver.Resolve(Request(skuId, list), list, price));

        Assert.Equal("NoApplicablePrice", exception.Code);
    }

    [Fact]
    public void Contract_list_without_matching_sku_price_has_no_applicable_price()
    {
        var (list, contract) = CreateActiveContract();
        var request = Request(Guid.NewGuid(), list, customerId: contract.CustomerId, contractId: contract.Id);

        var exception = Assert.Throws<DomainException>(() => PriceResolver.Resolve(request, list, null, contract: contract));

        Assert.Equal("NoApplicablePrice", exception.Code);
    }

    [Fact]
    public void Explicit_negotiated_price_never_falls_back_to_configured_price()
    {
        var list = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var configured = SkuPrice.Create(list, skuId);
        configured.SetInitialPrice(Now, [PriceTier.Create(1, 100)], Actor, Now);
        var negotiated = CreateNegotiated(skuId, list, amount: 80);
        var request = Request(skuId, list, customerId: negotiated.CustomerId, transactionId: negotiated.TransactionId, negotiatedPriceId: Guid.NewGuid());

        var exception = Assert.Throws<DomainException>(() => PriceResolver.Resolve(request, list, configured, negotiated));

        Assert.Equal("NegotiatedPriceMismatch", exception.Code);
    }

    [Fact]
    public void Valid_negotiated_price_resolves_without_configured_sku_price()
    {
        var list = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var negotiated = CreateNegotiated(skuId, list, amount: 80);
        var request = Request(skuId, list, quantity: negotiated.Quantity, customerId: negotiated.CustomerId, transactionId: negotiated.TransactionId, negotiatedPriceId: negotiated.Id);

        var result = PriceResolver.Resolve(request, list, null, negotiated);

        Assert.Equal(80, result.UnitPrice);
        Assert.Equal(negotiated.Id, result.NegotiatedPriceId);
        Assert.Null(result.SkuPriceId);
        Assert.Null(result.PricePeriodId);
    }

    [Fact]
    public void Negotiated_price_rejects_wrong_request_context_and_list()
    {
        var list = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var negotiated = CreateNegotiated(skuId, list);
        var request = Request(skuId, list, quantity: negotiated.Quantity, customerId: negotiated.CustomerId, transactionId: negotiated.TransactionId, negotiatedPriceId: negotiated.Id);

        AssertCode("NegotiatedTransactionMismatch", request with { TransactionId = Guid.NewGuid() }, list, negotiated);
        AssertCode("NegotiatedCustomerMismatch", request with { CustomerId = Guid.NewGuid() }, list, negotiated);
        AssertCode("PriceListCurrencyMismatch", request with { Currency = Currency.Create("USD") }, list, negotiated);
        AssertCode("PriceListTypeMismatch", request with { Channel = PriceListType.Retail }, list, negotiated);
    }

    [Fact]
    public void Contract_resolution_rejects_wrong_contract_context()
    {
        var skuId = Guid.NewGuid();
        var (list, contract, price) = CreateContractPrice(skuId);
        var request = Request(skuId, list, customerId: contract.CustomerId, contractId: contract.Id);

        AssertCode("ContractCustomerMismatch", request with { CustomerId = Guid.NewGuid() }, list, null, price, contract);
        AssertCode("ContractMismatch", request with { ContractId = Guid.NewGuid() }, list, null, price, contract);
        var wholesale = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        AssertCode("UnexpectedContractId", Request(skuId, wholesale, contractId: contract.Id), wholesale, null);
        var otherList = PriceList.Create("Other contract", Vnd, PriceListType.Contract);
        var otherContract = Contract.Create(contract.CustomerId, otherList, contract.Validity, "Net 30");
        otherContract.Activate(Actor, Now);
        AssertCode("PriceListMismatch", Request(skuId, otherList, customerId: otherContract.CustomerId, contractId: otherContract.Id), otherList, null, price, otherContract);
    }

    [Fact]
    public void Historical_resolution_uses_approval_revocation_and_termination_times()
    {
        var wholesale = PriceList.Create("Wholesale", Vnd, PriceListType.Wholesale);
        var skuId = Guid.NewGuid();
        var negotiated = CreateNegotiated(skuId, wholesale, approveAt: Now.AddHours(1), revokeAt: Now.AddDays(3));
        var negotiatedRequest = Request(skuId, wholesale, quantity: negotiated.Quantity, at: Now.AddDays(2), customerId: negotiated.CustomerId, transactionId: negotiated.TransactionId, negotiatedPriceId: negotiated.Id);
        Assert.Equal(80, PriceResolver.Resolve(negotiatedRequest, wholesale, null, negotiated).UnitPrice);
        AssertCode("NegotiatedPriceNotEffective", negotiatedRequest with { At = Now }, wholesale, negotiated);
        AssertCode("NegotiatedPriceNotEffective", negotiatedRequest with { At = Now.AddDays(3) }, wholesale, negotiated);

        var contractSkuId = Guid.NewGuid();
        var (contractList, contract, contractPrice) = CreateContractPrice(contractSkuId, Now.AddDays(3));
        var contractRequest = Request(contractSkuId, contractList, at: Now.AddDays(2), customerId: contract.CustomerId, contractId: contract.Id);
        Assert.Equal(70, PriceResolver.Resolve(contractRequest, contractList, contractPrice, contract: contract).UnitPrice);
        AssertCode("ContractNotEffective", contractRequest with { At = Now.AddDays(3) }, contractList, null, contractPrice, contract);
    }

    private static PriceResolutionRequest Request(Guid skuId, PriceList list, int quantity = 1, DateTimeOffset? at = null, Guid? customerId = null, Guid? transactionId = null, Guid? contractId = null, Guid? negotiatedPriceId = null) =>
        new(skuId, quantity, list.Currency, list.Type, at ?? Now, customerId, transactionId, contractId, negotiatedPriceId);

    private static NegotiatedPrice CreateNegotiated(Guid skuId, PriceList list, decimal amount = 80, DateTimeOffset? approveAt = null, DateTimeOffset? revokeAt = null)
    {
        var price = NegotiatedPrice.Create(Guid.NewGuid(), skuId, Guid.NewGuid(), 1, amount, list.Currency, list.Type, null, "Volume", EffectivePeriod.Create(Now, Now.AddDays(5)), Actor, Now.AddDays(-1));
        price.Submit(Actor, Now);
        price.Approve(Actor, approveAt ?? Now);
        if (revokeAt is not null)
        {
            price.Revoke(Actor, revokeAt.Value);
        }
        return price;
    }

    private static (PriceList List, Contract Contract) CreateActiveContract(DateTimeOffset? terminateAt = null)
    {
        var list = PriceList.Create("Contract", Vnd, PriceListType.Contract);
        var contract = Contract.Create(Guid.NewGuid(), list, EffectivePeriod.Create(Now, Now.AddDays(10)), "Net 30");
        contract.Activate(Actor, Now);
        if (terminateAt is not null)
        {
            contract.Terminate(Actor, terminateAt.Value);
        }
        return (list, contract);
    }

    private static (PriceList List, Contract Contract, SkuPrice Price) CreateContractPrice(Guid skuId, DateTimeOffset? terminateAt = null)
    {
        var list = PriceList.Create("Contract", Vnd, PriceListType.Contract);
        var contract = Contract.Create(Guid.NewGuid(), list, EffectivePeriod.Create(Now, Now.AddDays(10)), "Net 30");
        var price = SkuPrice.Create(list, skuId, contract);
        price.SetInitialPrice(Now, [PriceTier.Create(1, 70)], Actor, Now, contract);
        contract.Activate(Actor, Now);
        if (terminateAt is not null)
        {
            contract.Terminate(Actor, terminateAt.Value);
        }
        return (list, contract, price);
    }

    private static void AssertCode(string code, PriceResolutionRequest request, PriceList list, NegotiatedPrice? negotiatedPrice, SkuPrice? skuPrice = null, Contract? contract = null)
    {
        var exception = Assert.Throws<DomainException>(() => PriceResolver.Resolve(request, list, skuPrice, negotiatedPrice, contract));
        Assert.Equal(code, exception.Code);
    }
}