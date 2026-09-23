using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class ContractAmendmentTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Approved_amendment_changes_only_its_sku_from_the_agreed_instant()
    {
        var (contract, price) = Create();
        var proposal = price.ProposeContractPriceChange(contract, Start.AddDays(10), [PriceTier.Create(1, 120)], "Agreed new price", Actor, Start.AddDays(1));
        Assert.Equal(100, price.Resolve(1, Start.AddDays(10))!.Amount);
        price.ApproveContractPriceAmendment(contract, proposal, Actor, Start.AddDays(2));
        Assert.Equal(100, price.Resolve(1, Start.AddDays(10).AddTicks(-1))!.Amount);
        Assert.Equal(120, price.Resolve(1, Start.AddDays(10))!.Amount);
        Assert.Equal(proposal, price.Resolve(1, Start.AddDays(10))!.ContractPriceAmendmentId);
        Assert.Equal(ContractPriceAmendmentStatus.Approved, price.ContractPriceAmendments.Single().Status);
        Assert.Throws<DomainException>(() => price.ApproveContractPriceAmendment(contract, proposal, Actor, Start.AddDays(2)));
    }

    [Fact]
    public void Activated_contract_cannot_bypass_amendment_with_generic_price_operations()
    {
        var (contract, price) = Create();
        var stamp = price.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => price.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(1)));
        Assert.Throws<DomainException>(() => price.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(1), contract));
        Assert.Throws<DomainException>(() => price.SchedulePriceChange(Start.AddDays(10), [PriceTier.Create(1, 120)], Actor, Start.AddDays(1), contract));
        Assert.Equal(stamp, price.ConcurrencyStamp);
        Assert.Single(price.Periods);
    }

    [Fact]
    public void Failed_stale_or_late_approval_leaves_both_proposal_and_price_unchanged()
    {
        var (contract, price) = Create();
        var first = price.ProposeContractPriceChange(contract, Start.AddDays(10), [PriceTier.Create(1, 120)], "First", Actor, Start.AddDays(1));
        var stale = price.ProposeContractPriceChange(contract, Start.AddDays(20), [PriceTier.Create(1, 130)], "Second", Actor, Start.AddDays(1));
        price.ApproveContractPriceAmendment(contract, first, Actor, Start.AddDays(2));
        var stamp = price.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => price.ApproveContractPriceAmendment(contract, stale, Actor, Start.AddDays(3)));
        Assert.Equal(stamp, price.ConcurrencyStamp);
        Assert.Equal(ContractPriceAmendmentStatus.PendingApproval, price.ContractPriceAmendments.Single(value => value.Id == stale).Status);
        var late = price.ProposeContractPriceChange(contract, Start.AddDays(5), [PriceTier.Create(1, 140)], "Too late", Actor, Start.AddDays(3));
        stamp = price.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => price.ApproveContractPriceAmendment(contract, late, Actor, Start.AddDays(6)));
        Assert.Equal(stamp, price.ConcurrencyStamp);
        Assert.Equal(100, price.Resolve(1, Start.AddDays(6))!.Amount);
    }

    [Fact]
    public void Rescheduling_and_cancelling_approved_future_price_require_new_amendments()
    {
        var (contract, price) = Create();
        var proposal = price.ProposeContractPriceChange(contract, Start.AddDays(10), [PriceTier.Create(1, 120)], "New price", Actor, Start.AddDays(1));
        price.ApproveContractPriceAmendment(contract, proposal, Actor, Start.AddDays(2));
        var periodId = price.Periods[1].Id;
        Assert.Throws<DomainException>(() => price.CancelScheduledPrice(periodId, Actor, Start.AddDays(3), contract));
        var reschedule = price.ProposeContractPriceReschedule(contract, periodId, Start.AddDays(20), "Delay", Actor, Start.AddDays(3));
        price.ApproveContractPriceAmendment(contract, reschedule, Actor, Start.AddDays(4));
        Assert.Equal(100, price.Resolve(1, Start.AddDays(10))!.Amount);
        Assert.Equal(120, price.Resolve(1, Start.AddDays(20))!.Amount);
        var cancellation = price.ProposeContractPriceCancellation(contract, periodId, "Withdraw", Actor, Start.AddDays(5));
        price.ApproveContractPriceAmendment(contract, cancellation, Actor, Start.AddDays(6));
        Assert.Equal(100, price.Resolve(1, Start.AddDays(20))!.Amount);
        Assert.Equal(Start.AddDays(10), price.ContractPriceAmendments.Single(value => value.Id == proposal).EffectiveFrom);
        Assert.Equal(Start.AddDays(20), price.ContractPriceAmendments.Single(value => value.Id == reschedule).EffectiveFrom);
        Assert.Single(price.Periods);
    }

    [Fact]
    public void Changed_or_terminated_contract_prevents_approval_without_losing_proposal()
    {
        var (contract, price) = Create();
        var proposal = price.ProposeContractPriceChange(contract, Start.AddDays(10), [PriceTier.Create(1, 120)], "New", Actor, Start.AddDays(1));
        contract.Terminate(Actor, Start.AddDays(2));
        var stamp = price.ConcurrencyStamp;
        Assert.Throws<DomainException>(() => price.ApproveContractPriceAmendment(contract, proposal, Actor, Start.AddDays(3)));
        Assert.Equal(stamp, price.ConcurrencyStamp);
        Assert.Single(price.Periods);
    }

    private static (Contract Contract, SkuPrice Price) Create()
    {
        var list = PriceList.Create("Contract VN", Currency.Create("VND"), PriceListType.Contract);
        var contract = Contract.Create(Guid.NewGuid(), list, EffectivePeriod.Create(Start, Start.AddYears(1)), "Payment within 30 days");
        var price = SkuPrice.Create(list, Guid.NewGuid(), contract);
        price.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start, contract);
        contract.Activate(Actor, Start);
        return (contract, price);
    }
}