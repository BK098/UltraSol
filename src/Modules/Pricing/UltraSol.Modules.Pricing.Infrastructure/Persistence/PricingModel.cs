using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;

namespace UltraSol.Modules.Pricing.Infrastructure.Persistence;

internal static class PricingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema.Name);
        model.HasPostgresExtension("btree_gist");
        var lists = Root<PriceList>(model, "price_lists");
        lists.Property(x => x.Name).HasMaxLength(250);
        Currency(lists.Property(x => x.Currency));
        lists.Property(x => x.Type);

        var contracts = Root<Contract>(model, "contracts");
        contracts.Property(x => x.CustomerId);
        contracts.Property(x => x.PriceListId);
        Currency(contracts.Property(x => x.Currency));
        contracts.Property(x => x.CommercialTerms);
        contracts.OwnsOne(x => x.Validity, validity =>
        {
            validity.Property<Guid>("ContractId").HasColumnName("id");
            validity.Property(x => x.From).HasColumnName("valid_from");
            validity.Property(x => x.To).HasColumnName("valid_to");
        });
        contracts.Navigation(x => x.Validity).IsRequired();
        contracts.HasIndex(x => x.PriceListId).IsUnique();
        contracts.HasIndex(x => new { x.CustomerId, x.Status });
        contracts.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);

        var prices = Root<SkuPrice>(model, "sku_prices");
        prices.Ignore(x => x.Periods);
        prices.Ignore(x => x.Changes);
        prices.Ignore(x => x.ContractPriceAmendments);
        prices.Property(x => x.PriceListId);
        prices.Property(x => x.SkuId);
        Currency(prices.Property(x => x.Currency));
        prices.Property(x => x.Type);
        prices.Property(x => x.ContractId);
        prices.Property<DateTimeOffset?>("_lastMutationAt").HasColumnName("last_mutation_at");
        prices.HasIndex(x => new { x.PriceListId, x.SkuId }).IsUnique();
        prices.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
        prices.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);

        var negotiations = Root<NegotiatedPrice>(model, "negotiated_prices");
        negotiations.Property(x => x.CustomerId);
        negotiations.Property(x => x.SkuId);
        negotiations.Property(x => x.TransactionId);
        negotiations.Property(x => x.Quantity);
        negotiations.Property(x => x.Amount).HasColumnType("numeric");
        Currency(negotiations.Property(x => x.Currency));
        negotiations.Property(x => x.Channel);
        negotiations.Property(x => x.ContractId);
        negotiations.Property(x => x.Reason);
        negotiations.Property(x => x.ProposedBy);
        negotiations.Property(x => x.ProposedAt);
        negotiations.OwnsOne(x => x.Validity, validity =>
        {
            validity.Property<Guid>("NegotiatedPriceId").HasColumnName("id");
            validity.Property(x => x.From).HasColumnName("valid_from");
            validity.Property(x => x.To).HasColumnName("valid_to");
        });
        negotiations.Navigation(x => x.Validity).IsRequired();
        negotiations.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        negotiations.HasIndex(x => new { x.CustomerId, x.Status });
        negotiations.HasIndex(x => x.TransactionId);
        negotiations.HasIndex(x => x.SkuId);

        var periods = model.Entity<PricePeriodRow>();
        periods.ToTable("price_periods", table => table.HasCheckConstraint("ck_price_periods_range", "effective_to IS NULL OR effective_to > effective_from"));
        periods.HasKey(x => x.Id);
        periods.Property(x => x.Id).ValueGeneratedNever();
        periods.HasIndex(x => new { x.SkuPriceId, x.EffectiveFrom });
        periods.HasOne<SkuPrice>().WithMany().HasForeignKey(x => x.SkuPriceId).OnDelete(DeleteBehavior.Restrict);

        var tiers = model.Entity<PriceTierRow>();
        tiers.ToTable("price_tiers", table => table.HasCheckConstraint("ck_price_tiers_values", "minimum_quantity > 0 AND amount >= 0"));
        tiers.HasKey(x => new { x.PricePeriodId, x.MinimumQuantity });
        tiers.Property(x => x.Amount).HasColumnType("numeric");
        tiers.HasOne<PricePeriodRow>().WithMany().HasForeignKey(x => x.PricePeriodId).OnDelete(DeleteBehavior.Cascade);

        var changes = model.Entity<PriceChangeRow>();
        changes.ToTable("price_changes");
        changes.HasKey(x => new { x.SkuPriceId, x.Revision });
        changes.Property(x => x.BeforeJson).HasColumnType("jsonb");
        changes.Property(x => x.AfterJson).HasColumnType("jsonb");
        changes.HasOne<SkuPrice>().WithMany().HasForeignKey(x => x.SkuPriceId).OnDelete(DeleteBehavior.Restrict);

        var amendments = model.Entity<ContractPriceAmendmentRow>();
        amendments.ToTable("contract_price_amendments");
        amendments.HasKey(x => x.Id);
        amendments.Property(x => x.Id).ValueGeneratedNever();
        amendments.Property(x => x.ProposalJson).HasColumnType("jsonb");
        amendments.HasIndex(x => new { x.SkuPriceId, x.ProposedAt });
        amendments.HasOne<SkuPrice>().WithMany().HasForeignKey(x => x.SkuPriceId).OnDelete(DeleteBehavior.Restrict);
        amendments.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        periods.HasOne<ContractPriceAmendmentRow>().WithMany().HasForeignKey(x => x.ContractPriceAmendmentId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName() == property.Name)
                {
                    property.SetColumnName(ModelConfigure.Snake(property.Name));
                }
                if (!property.IsPrimaryKey() && property.PropertyInfo?.SetMethod is null && property.FieldInfo is not null)
                {
                    property.SetPropertyAccessMode(PropertyAccessMode.Field);
                }
            }
        }
    }

    private static EntityTypeBuilder<T> Root<T>(ModelBuilder model, string table)
        where T : AggregateRoot => ModelConfigure.Root<T>(model, table);

    private static void Currency(PropertyBuilder<Currency> property) => property.HasConversion(value => value.Code, value => Domain.ValueObjects.Currency.Create(value)).HasMaxLength(3);
}