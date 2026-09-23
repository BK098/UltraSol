using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PricingPostgresTests(PricingDatabaseFixture fixture) : IClassFixture<PricingDatabaseFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [PricingPostgresFact]
    public async Task Roundtrip_restores_timeline_tiers_audit_revision_and_last_mutation()
    {
        var (list, sku, _) = await Seed();
        Guid future;
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                future = price.SchedulePriceChange(Start.AddDays(20), [PriceTier.Create(1, 120)], Actor, Start.AddDays(1));
                price.ChangePriceImmediately([PriceTier.Create(1, 100), PriceTier.Create(50, 85)], Actor, Start.AddDays(2));
                price.ChangeScheduledPrice(future, [PriceTier.Create(1, 125)], Actor, Start.AddDays(3));
            });
        }
        await using var restored = fixture.Create();
        var loaded = await new SkuPriceRepository(restored).GetRequiredByIdAsync(sku.Id);
        Assert.Equal(list.Id, loaded.PriceListId);
        Assert.Equal(sku.SkuId, loaded.SkuId);
        Assert.Equal(4, loaded.Revision);
        Assert.Equal(4, loaded.Changes.Count);
        Assert.Equal(3, loaded.Periods.Count);
        Assert.Equal(85, loaded.Resolve(60, Start.AddDays(2))!.Amount);
        Assert.Equal(125, loaded.Resolve(60, Start.AddDays(20))!.Amount);
        Assert.Equal(100, loaded.Changes[0].After.Single().Tiers.Single().Amount);
        Assert.Equal(120, loaded.Changes[3].Before.Last().Tiers.Single().Amount);
        Assert.Throws<DomainException>(() => loaded.ChangePriceImmediately([PriceTier.Create(1, 90)], Actor, Start.AddDays(2)));
    }

    [PricingPostgresFact]
    public async Task Approved_amendment_roundtrips_and_failed_approval_rolls_back()
    {
        var (_, sku, contract) = await Seed(PriceListType.Contract);
        Guid first = default;
        Guid stale = default;
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var agreement = await new ContractRepository(db).GetLockedRequiredAsync(contract!.Id, true, ct);
                agreement.Activate(Actor, Start);
            });
        }
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var agreement = await new ContractRepository(db).GetLockedRequiredAsync(contract!.Id, false, ct);
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                first = price.ProposeContractPriceChange(agreement, Start.AddDays(10), [PriceTier.Create(1, 110)], "First", Actor, Start.AddDays(1));
                stale = price.ProposeContractPriceChange(agreement, Start.AddDays(20), [PriceTier.Create(1, 120)], "Stale", Actor, Start.AddDays(1));
            });
        }
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var agreement = await new ContractRepository(db).GetLockedRequiredAsync(contract!.Id, false, ct);
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                price.ApproveContractPriceAmendment(agreement, first, Actor, Start.AddDays(2));
            });
        }
        await using (var db = fixture.Create())
        {
            await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var agreement = await new ContractRepository(db).GetLockedRequiredAsync(contract!.Id, false, ct);
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                price.ApproveContractPriceAmendment(agreement, stale, Actor, Start.AddDays(3));
            }));
        }
        await using var check = fixture.Create();
        var loaded = await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id);
        var approved = loaded.ContractPriceAmendments.Single(x => x.Id == first);
        Assert.Equal(ContractPriceAmendmentStatus.Approved, approved.Status);
        Assert.Equal(Actor, approved.DecidedBy);
        Assert.Equal(Start.AddDays(2), approved.DecidedAt);
        Assert.Equal("First", approved.Reason);
        Assert.Equal(ContractPriceAmendmentStatus.PendingApproval, loaded.ContractPriceAmendments.Single(x => x.Id == stale).Status);
        Assert.Equal(first, loaded.Resolve(1, Start.AddDays(10))!.ContractPriceAmendmentId);
        Assert.Equal(2, loaded.Changes.Count);
        await using var tamper = fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(tamper).ExecuteInTransactionAsync(ct =>
            tamper.Database.ExecuteSqlInterpolatedAsync($"UPDATE pricing.contract_price_amendments SET proposed_at = proposed_at + INTERVAL '1 day' WHERE id = {first}", ct)));
    }

    [PricingPostgresFact]
    public async Task Same_sku_concurrent_writes_conflict_without_losing_winner()
    {
        var (_, sku, _) = await Seed();
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        var a = await new SkuPriceRepository(first).GetTrackedRequiredAsync(sku.Id);
        var b = await new SkuPriceRepository(second).GetTrackedRequiredAsync(sku.Id);
        await PricingDatabaseFixture.Unit(first).ExecuteInTransactionAsync(ct =>
        {
            a.ChangePriceImmediately([PriceTier.Create(1, 110)], Actor, Start.AddDays(1));
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(second).ExecuteInTransactionAsync(ct =>
        {
            b.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(2));
            return Task.CompletedTask;
        }));
        await using var check = fixture.Create();
        var loaded = await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id);
        Assert.Equal(110, loaded.Resolve(1, Start.AddDays(2))!.Amount);
        Assert.Equal(2, loaded.Changes.Count);
    }

    [PricingPostgresFact]
    public async Task Reschedule_and_cancellation_synchronize_replaced_periods_and_removed_tiers()
    {
        var (_, sku, _) = await Seed();
        Guid future = default;
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                future = price.SchedulePriceChange(Start.AddDays(10), [PriceTier.Create(1, 110), PriceTier.Create(50, 90)], Actor, Start.AddDays(1));
                price.SchedulePriceChange(Start.AddDays(20), [PriceTier.Create(1, 120)], Actor, Start.AddDays(1));
            });
        }
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                price.ReschedulePriceChange(future, Start.AddDays(15), Actor, Start.AddDays(2));
                price.ChangeScheduledPrice(future, [PriceTier.Create(1, 115)], Actor, Start.AddDays(2));
            });
        }
        await using (var check = fixture.Create())
        {
            var price = await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id);
            Assert.Equal(100, price.Resolve(60, Start.AddDays(10))!.Amount);
            Assert.Equal(115, price.Resolve(60, Start.AddDays(15))!.Amount);
            Assert.Single(price.Periods.Single(period => period.Id == future).Tiers);
        }
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                var price = await new SkuPriceRepository(db).GetTrackedRequiredAsync(sku.Id, ct);
                price.CancelScheduledPrice(future, Actor, Start.AddDays(3));
            });
        }
        await using var final = fixture.Create();
        var loaded = await new SkuPriceRepository(final).GetRequiredByIdAsync(sku.Id);
        Assert.Equal(100, loaded.Resolve(60, Start.AddDays(15))!.Amount);
        Assert.Equal(120, loaded.Resolve(60, Start.AddDays(20))!.Amount);
        Assert.Equal(6, loaded.Changes.Count);
        Assert.False(await final.PriceTiers.AnyAsync(tier => tier.PricePeriodId == future));
    }

    [PricingPostgresFact]
    public async Task Different_skus_in_one_list_commit_independently()
    {
        var (list, a, _) = await Seed();
        var b = SkuPrice.Create(list, Guid.NewGuid());
        b.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start);
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(ct => new SkuPriceRepository(db).AddAsync(b, ct));
        }
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        await using var transaction = await PricingDatabaseFixture.Unit(first).BeginTransactionAsync();
        var one = await new SkuPriceRepository(first).GetTrackedRequiredAsync(a.Id);
        one.ChangePriceImmediately([PriceTier.Create(1, 110)], Actor, Start.AddDays(1));
        await first.SaveChangesAsync();
        await PricingDatabaseFixture.Unit(second).ExecuteInTransactionAsync(async ct =>
        {
            var two = await new SkuPriceRepository(second).GetTrackedRequiredAsync(b.Id, ct);
            two.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(1));
        }).WaitAsync(TimeSpan.FromSeconds(10));
        await transaction.CommitAsync();
        await using var check = fixture.Create();
        Assert.Equal(110, (await new SkuPriceRepository(check).GetRequiredByIdAsync(a.Id)).Resolve(1, Start.AddDays(1))!.Amount);
        Assert.Equal(120, (await new SkuPriceRepository(check).GetRequiredByIdAsync(b.Id)).Resolve(1, Start.AddDays(1))!.Amount);
    }

    [PricingPostgresFact]
    public async Task Unique_sku_pair_and_contract_list_are_database_enforced()
    {
        var (list, sku, _) = await Seed();
        await using (var db = fixture.Create())
        {
            await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(ct =>
                new SkuPriceRepository(db).AddAsync(SkuPrice.Create(list, sku.SkuId), ct)));
        }
        var (contractList, _, _) = await Seed(PriceListType.Contract);
        await using var duplicate = fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(duplicate).ExecuteInTransactionAsync(ct =>
            new ContractRepository(duplicate).AddAsync(Contract.Create(Guid.NewGuid(), contractList, EffectivePeriod.Create(Start), "Duplicate"), ct)));
    }

    [PricingPostgresFact]
    public async Task Negotiation_preserves_context_and_approval_history()
    {
        var negotiated = NegotiatedPrice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 60, 85, Currency.Create("VND"),
            PriceListType.Wholesale, null, "Agreed", EffectivePeriod.Create(Start, Start.AddDays(30)), Actor, Start);
        await using (var db = fixture.Create())
        {
            await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
            {
                await new NegotiatedPriceRepository(db).AddAsync(negotiated, ct);
                negotiated.Submit(Actor, Start.AddHours(1));
                negotiated.Approve(Actor, Start.AddHours(2));
            });
        }
        await using var check = fixture.Create();
        var loaded = await new NegotiatedPriceRepository(check).GetRequiredByIdAsync(negotiated.Id);
        Assert.Equal(negotiated.TransactionId, loaded.TransactionId);
        Assert.Equal(negotiated.CustomerId, loaded.CustomerId);
        Assert.Equal(negotiated.Validity, loaded.Validity);
        Assert.Equal(NegotiatedPriceStatus.Approved, loaded.Status);
        Assert.Equal(Actor, loaded.ApprovedBy);
        Assert.Equal(Start.AddHours(2), loaded.ApprovedAt);
        Assert.Equal(Actor, loaded.ProposedBy);
    }

    [PricingPostgresFact]
    public async Task Overlap_is_deferred_until_commit_and_rolls_back_the_whole_operation()
    {
        var (list, sku, _) = await Seed();
        await using var db = fixture.Create();
        var inserted = false;
        var error = await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await new PriceListRepository(db).GetTrackedRequiredAsync(list.Id, ct);
            tracked.Rename("Must rollback");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO pricing.price_periods (id, sku_price_id, effective_from) VALUES ({Guid.NewGuid()}, {sku.Id}, {Start.AddDays(1)})", ct);
            inserted = true;
        }));
        Assert.True(inserted);
        Assert.Equal("OverlappingPricePeriods", error.Code);
        await using var check = fixture.Create();
        Assert.Equal(list.Name, (await new PriceListRepository(check).GetRequiredByIdAsync(list.Id)).Name);
        Assert.Single((await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id)).Periods);
    }

    [PricingPostgresFact]
    public async Task Audit_and_decided_amendments_cannot_be_overwritten_or_bulk_deleted()
    {
        var (_, sku, _) = await Seed();
        await using var db = fixture.Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SkuPriceRepository(db).ExecuteDeleteAsync(x => x.Id == sku.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SkuPriceRepository(db).ExecuteUpdateAsync(x => x.Id == sku.Id, update => update.Set(x => x.Revision, 99)));
        await Assert.ThrowsAsync<DomainException>(() => PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(ct =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE pricing.price_changes SET actor_id = {Guid.NewGuid()} WHERE sku_price_id = {sku.Id}", ct)));
        await using var check = fixture.Create();
        Assert.Equal(Actor, (await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id)).Changes.Single().ActorId);
    }

    [PricingPostgresFact]
    public async Task Activation_and_termination_winners_are_seen_by_waiting_price_writes()
    {
        foreach (var terminate in new[] { false, true })
        {
            var (_, sku, contract) = await Seed(PriceListType.Contract);
            Guid proposal = default;
            if (terminate)
            {
                await using var setup = fixture.Create();
                await PricingDatabaseFixture.Unit(setup).ExecuteInTransactionAsync(async ct =>
                {
                    var agreement = await new ContractRepository(setup).GetLockedRequiredAsync(contract!.Id, true, ct);
                    agreement.Activate(Actor, Start);
                    var price = await new SkuPriceRepository(setup).GetTrackedRequiredAsync(sku.Id, ct);
                    proposal = price.ProposeContractPriceChange(agreement, Start.AddDays(10), [PriceTier.Create(1, 120)], "Pending", Actor, Start.AddDays(1));
                });
            }
            await using var transitionDb = fixture.Create();
            await using var priceDb = fixture.Create();
            // Deliberately cache the old contract before the lock: the locked read must refresh it.
            await new ContractRepository(priceDb).GetTrackedRequiredAsync(contract!.Id);
            await using var transition = await PricingDatabaseFixture.Unit(transitionDb).BeginTransactionAsync();
            var changingContract = await new ContractRepository(transitionDb).GetLockedRequiredAsync(contract.Id, true);
            if (terminate)
            {
                changingContract.Terminate(Actor, Start.AddDays(2));
            }
            else
            {
                changingContract.Activate(Actor, Start.AddDays(2));
            }
            await transitionDb.SaveChangesAsync();
            var priceWrite = PricingDatabaseFixture.Unit(priceDb).ExecuteInTransactionAsync(async ct =>
            {
                var latest = await new ContractRepository(priceDb).GetLockedRequiredAsync(contract.Id, false, ct);
                var price = await new SkuPriceRepository(priceDb).GetTrackedRequiredAsync(sku.Id, ct);
                if (terminate)
                {
                    price.ApproveContractPriceAmendment(latest, proposal, Actor, Start.AddDays(3));
                }
                else
                {
                    price.ChangePriceImmediately([PriceTier.Create(1, 120)], Actor, Start.AddDays(3), latest);
                }
            });
            await WaitForDatabaseLock();
            Assert.False(priceWrite.IsCompleted);
            await transition.CommitAsync();
            await Assert.ThrowsAsync<DomainException>(() => priceWrite.WaitAsync(TimeSpan.FromSeconds(10)));
            await using var check = fixture.Create();
            var loaded = await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id);
            Assert.Single(loaded.Periods);
            if (terminate)
            {
                Assert.Equal(ContractPriceAmendmentStatus.PendingApproval, loaded.ContractPriceAmendments.Single().Status);
            }
        }
    }

    [PricingPostgresFact]
    public async Task Shared_contract_lock_allows_other_skus_but_delays_activation()
    {
        var (list, sku, contract) = await Seed(PriceListType.Contract);
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        await using var transitionDb = fixture.Create();
        await using var transaction = await PricingDatabaseFixture.Unit(first).BeginTransactionAsync();
        var agreement = await new ContractRepository(first).GetLockedRequiredAsync(contract!.Id, false);
        var price = await new SkuPriceRepository(first).GetTrackedRequiredAsync(sku.Id);
        price.ChangePriceImmediately([PriceTier.Create(1, 110)], Actor, Start.AddDays(1), agreement);
        await first.SaveChangesAsync();
        await PricingDatabaseFixture.Unit(second).ExecuteInTransactionAsync(async ct =>
        {
            var shared = await new ContractRepository(second).GetLockedRequiredAsync(contract.Id, false, ct);
            var other = SkuPrice.Create(list, Guid.NewGuid(), shared);
            other.SetInitialPrice(Start.AddDays(1), [PriceTier.Create(1, 120)], Actor, Start.AddDays(1), shared);
            await new SkuPriceRepository(second).AddAsync(other, ct);
        }).WaitAsync(TimeSpan.FromSeconds(10));
        var activation = PricingDatabaseFixture.Unit(transitionDb).ExecuteInTransactionAsync(async ct =>
        {
            var latest = await new ContractRepository(transitionDb).GetLockedRequiredAsync(contract.Id, true, ct);
            latest.Activate(Actor, Start.AddDays(2));
        });
        await WaitForDatabaseLock();
        Assert.False(activation.IsCompleted);
        await transaction.CommitAsync();
        await activation.WaitAsync(TimeSpan.FromSeconds(10));
        await using var check = fixture.Create();
        Assert.Equal(110, (await new SkuPriceRepository(check).GetRequiredByIdAsync(sku.Id)).Resolve(1, Start.AddDays(3))!.Amount);
        Assert.Equal(ContractStatus.Active, (await new ContractRepository(check).GetRequiredByIdAsync(contract.Id)).Status);
    }

    private async Task WaitForDatabaseLock()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock')", connection);
        while (!(bool)(await command.ExecuteScalarAsync(timeout.Token))!)
        {
            await Task.Delay(25, timeout.Token);
        }
    }

    private async Task<(PriceList List, SkuPrice Sku, Contract? Contract)> Seed(PriceListType type = PriceListType.Wholesale)
    {
        var list = PriceList.Create("Test " + Guid.NewGuid(), Currency.Create("VND"), type);
        var contract = type == PriceListType.Contract ? Contract.Create(Guid.NewGuid(), list, EffectivePeriod.Create(Start, Start.AddYears(1)), "Terms") : null;
        var sku = SkuPrice.Create(list, Guid.NewGuid(), contract);
        sku.SetInitialPrice(Start, [PriceTier.Create(1, 100)], Actor, Start, contract);
        await using var db = fixture.Create();
        await PricingDatabaseFixture.Unit(db).ExecuteInTransactionAsync(async ct =>
        {
            await new PriceListRepository(db).AddAsync(list, ct);
            if (contract is not null)
            {
                await new ContractRepository(db).AddAsync(contract, ct);
            }
            await new SkuPriceRepository(db).AddAsync(sku, ct);
        });
        return (list, sku, contract);
    }
}