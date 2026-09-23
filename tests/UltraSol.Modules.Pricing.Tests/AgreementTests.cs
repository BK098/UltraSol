using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class AgreementTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly Currency Vnd = Currency.Create("VND");

    [Fact]
    public void Contract_requires_contract_list_and_freezes_terms()
    {
        var contractList = PriceList.Create("Contract", Vnd, PriceListType.Contract);
        var contract = Contract.Create(Guid.NewGuid(), contractList, EffectivePeriod.Create(Now.AddDays(1), Now.AddDays(10)), "Net 30");

        Assert.Equal(contractList.Id, contract.PriceListId);
        Assert.Equal(Vnd, contract.Currency);
        Assert.Equal("Net 30", contract.CommercialTerms);
        Assert.Throws<DomainException>(() => Contract.Create(Guid.Empty, contractList, contract.Validity, "Net 30"));
        Assert.Throws<DomainException>(() => Contract.Create(Guid.NewGuid(), PriceList.Create("Retail", Vnd, PriceListType.Retail), contract.Validity, "Net 30"));
        Assert.Throws<DomainException>(() => Contract.Create(Guid.NewGuid(), contractList, contract.Validity, " "));
    }

    [Fact]
    public void Contract_can_activate_before_start_and_is_historically_effective_until_termination()
    {
        var actor = Guid.NewGuid();
        var contract = CreateContract();
        contract.Activate(actor, Now);

        Assert.Equal(ContractStatus.Active, contract.Status);
        Assert.Equal(actor, contract.ActivatedBy);
        Assert.False(contract.IsEffectiveAt(Now));
        Assert.True(contract.IsEffectiveAt(Now.AddDays(1)));

        contract.Terminate(actor, Now.AddDays(3));

        Assert.True(contract.IsEffectiveAt(Now.AddDays(2)));
        Assert.False(contract.IsEffectiveAt(Now.AddDays(3)));
    }

    [Fact]
    public void Contract_rejects_expired_activation_and_invalid_amendment_without_mutation()
    {
        var contract = CreateContract();
        var stamp = contract.ConcurrencyStamp;

        Assert.Throws<DomainException>(() => contract.Activate(Guid.NewGuid(), Now.AddDays(10)));
        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.Null(contract.ActivatedAt);
        Assert.Equal(stamp, contract.ConcurrencyStamp);

        contract.Activate(Guid.NewGuid(), Now);
        stamp = contract.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => contract.EnsureCanAmend(Guid.NewGuid(), Now.AddDays(2), Now));
        Assert.Throws<DomainException>(() => contract.EnsureCanAmend(contract.PriceListId, Now.AddHours(-1), Now));
        Assert.Throws<DomainException>(() => contract.EnsureCanAmend(contract.PriceListId, Now.AddDays(2), Now.AddHours(-1)));
        Assert.Throws<DomainException>(() => contract.EnsureCanAmend(contract.PriceListId, Now.AddDays(10), Now));
        Assert.Equal(stamp, contract.ConcurrencyStamp);
        contract.EnsureCanAmend(contract.PriceListId, Now.AddDays(2), Now);
    }

    [Fact]
    public void Contract_cannot_be_soft_deleted()
    {
        Assert.Throws<DomainException>(() => CreateContract().MarkDeleted(Guid.NewGuid().ToString(), Now));
    }

    [Fact]
    public void Negotiated_price_requires_non_retail_channel_finite_validity_and_matching_contract_id_rule()
    {
        var proposedBy = Guid.NewGuid();
        var price = NegotiatedPrice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 100, Vnd, PriceListType.Wholesale, null, "Volume commitment", EffectivePeriod.Create(Now.AddDays(1), Now.AddDays(5)), proposedBy, Now);

        Assert.Equal(proposedBy, price.ProposedBy);
        Assert.Equal(Now, price.ProposedAt);
        Assert.Equal(proposedBy.ToString(), price.CreatedBy);
        Assert.Throws<DomainException>(() => CreateNegotiated(PriceListType.Retail));
        Assert.Throws<DomainException>(() => CreateNegotiated(PriceListType.Wholesale, validity: EffectivePeriod.Create(Now)));
        Assert.Throws<DomainException>(() => CreateNegotiated(PriceListType.Contract));
        Assert.Throws<DomainException>(() => CreateNegotiated(PriceListType.Wholesale, contractId: Guid.NewGuid()));
        Assert.Throws<DomainException>(() => CreateNegotiated(PriceListType.Contract, contractId: Guid.Empty));
        Assert.Throws<DomainException>(() => CreateNegotiated(quantity: 0));
        Assert.Throws<DomainException>(() => CreateNegotiated(amount: -1));
        Assert.Throws<DomainException>(() => CreateNegotiated(validity: EffectivePeriod.Create(Now.AddDays(-2), Now.AddDays(-1))));
    }

    [Fact]
    public void Negotiated_price_transition_uses_immutable_proposal_time()
    {
        var price = CreateNegotiated();
        price.CreatedAt = Now.AddDays(-2);

        Assert.Throws<DomainException>(() => price.Submit(Guid.NewGuid(), Now.AddHours(-1)));
        Assert.Equal(NegotiatedPriceStatus.Draft, price.Status);
        Assert.Null(price.SubmittedAt);
    }

    [Fact]
    public void Negotiated_price_requires_explicit_approval_and_preserves_history_after_revocation()
    {
        var actor = Guid.NewGuid();
        var price = CreateNegotiated();
        var context = ContextFor(price, Now.AddDays(2));

        Assert.Throws<DomainException>(() => price.EnsureApplicable(context));
        price.Submit(actor, Now);
        price.Approve(actor, Now.AddHours(1));

        price.EnsureApplicable(context);
        Assert.True(price.IsEffectiveAt(Now.AddDays(2)));
        Assert.False(price.IsEffectiveAt(Now));

        price.Revoke(actor, Now.AddDays(3));

        price.EnsureApplicable(ContextFor(price, Now.AddDays(2)));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(ContextFor(price, Now.AddDays(3))));
    }

    [Fact]
    public void Negotiated_price_can_be_rejected_only_from_pending_and_failed_transition_is_atomic()
    {
        var price = CreateNegotiated();
        var stamp = price.ConcurrencyStamp;

        Assert.Throws<DomainException>(() => price.Approve(Guid.NewGuid(), Now));
        Assert.Equal(NegotiatedPriceStatus.Draft, price.Status);
        Assert.Null(price.ApprovedAt);
        Assert.Equal(stamp, price.ConcurrencyStamp);

        price.Submit(Guid.NewGuid(), Now);
        price.Reject(Guid.NewGuid(), Now.AddHours(1));
        Assert.Equal(NegotiatedPriceStatus.Rejected, price.Status);
        Assert.Throws<DomainException>(() => price.Submit(Guid.NewGuid(), Now.AddHours(2)));
    }

    [Fact]
    public void Negotiated_price_approval_uses_half_open_expiry_boundary()
    {
        var price = CreateNegotiated();
        price.Submit(Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => price.Approve(Guid.NewGuid(), Now.AddDays(5)));
        Assert.Equal(NegotiatedPriceStatus.PendingApproval, price.Status);
        Assert.Null(price.ApprovedAt);
    }

    [Fact]
    public void Negotiated_price_rejects_expired_transition_without_mutation()
    {
        var price = CreateNegotiated();
        price.Submit(Guid.NewGuid(), Now);
        var stamp = price.ConcurrencyStamp;

        Assert.Throws<DomainException>(() => price.Reject(Guid.NewGuid(), Now.AddDays(5)));
        Assert.Equal(NegotiatedPriceStatus.PendingApproval, price.Status);
        Assert.Null(price.RejectedAt);
        Assert.Equal(stamp, price.ConcurrencyStamp);
    }

    [Fact]
    public void Negotiated_price_requires_every_context_dimension_to_match()
    {
        var price = CreateNegotiated();
        price.Submit(Guid.NewGuid(), Now);
        price.Approve(Guid.NewGuid(), Now);
        var context = ContextFor(price, Now.AddDays(2));

        price.EnsureApplicable(context);
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { SkuId = Guid.NewGuid() }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { CustomerId = Guid.NewGuid() }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { TransactionId = Guid.NewGuid() }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { Quantity = 2 }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { Currency = Currency.Create("USD") }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { Channel = PriceListType.Contract, ContractId = Guid.NewGuid() }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { ContractId = Guid.NewGuid() }));
        Assert.Throws<DomainException>(() => price.EnsureApplicable(context with { Quantity = 0 }));
    }

    [Fact]
    public void Negotiated_price_cannot_be_soft_deleted()
    {
        Assert.Throws<DomainException>(() => CreateNegotiated().MarkDeleted(Guid.NewGuid().ToString(), Now));
    }

    private static Contract CreateContract() => Contract.Create(Guid.NewGuid(), PriceList.Create("Contract", Vnd, PriceListType.Contract), EffectivePeriod.Create(Now.AddDays(1), Now.AddDays(10)), "Net 30");

    private static NegotiatedPrice CreateNegotiated(PriceListType channel = PriceListType.Wholesale, Guid? contractId = null, int quantity = 1, decimal amount = 100, EffectivePeriod? validity = null) =>
        NegotiatedPrice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity, amount, Vnd, channel, contractId, "Volume commitment", validity ?? EffectivePeriod.Create(Now.AddDays(1), Now.AddDays(5)), Guid.NewGuid(), Now);

    private static NegotiatedPriceContext ContextFor(NegotiatedPrice price, DateTimeOffset at) =>
        new(price.SkuId, price.CustomerId, price.TransactionId, price.Quantity, price.Currency, price.Channel, at, price.ContractId);
}