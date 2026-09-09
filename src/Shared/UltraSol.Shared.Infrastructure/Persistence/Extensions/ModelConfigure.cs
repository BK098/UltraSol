using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Shared.Infrastructure.Persistence.Extensions
{
    public class ModelConfigure<TKey> where TKey : notnull
    {
        public static EntityTypeBuilder<TEntity> Entity<TEntity>(ModelBuilder m, string table)
            where TEntity : BaseEntity<TKey>
        {
            var b = m.Entity<TEntity>();
            b.HasBaseType((Type?)null);
            b.ToTable(table);
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            return b;
        }

        public static EntityTypeBuilder<TEntity> Root<TEntity>(ModelBuilder m, string table)
            where TEntity : AggregateRoot<TKey>
        {
            var b = Entity<TEntity>(m, table);
            b.Ignore(x => x.DomainEvents); b.Ignore(x => x.IsDeleted);
            b.Ignore(x => x.DeletedAt); b.Ignore(x => x.DeletedBy);
            b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken().IsRequired();
            return b;
        }
        public static void Media<TEntity>(ModelBuilder m, string table, string owner)
            where TEntity : BaseEntity<TKey>
        {
            var b = Entity<TEntity>(m, table);
            b.Property<Guid>(owner);
            b.HasIndex(owner).IsUnique().HasFilter("is_primary");
            b.HasIndex(owner, "SortOrder");
            b.ToTable(table, t => t.HasCheckConstraint("ck_" + table + "_order", "sort_order >= 0"));
        }
        public static string Snake(string value) =>
            string.Concat(value.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
    public class ModelConfigure : ModelConfigure<Guid>;
}