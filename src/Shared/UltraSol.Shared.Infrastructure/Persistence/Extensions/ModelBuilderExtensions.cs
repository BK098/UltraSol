using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;
using System.Reflection;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Shared.Infrastructure.Persistence.Extensions;

public static class ModelBuilderExtensions
{
    private static readonly MethodInfo ApplySoftDeleteFilterMethod = typeof(ModelBuilderExtensions).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ModelBuilder ApplyAuditConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType is { IsAbstract: true } || clrType.IsInterface)
            {
                continue;
            }

            var builder = modelBuilder.Entity(clrType);
            ConfigureEntity(builder, entityType, clrType);

            if (typeof(ISoftDeleted).IsAssignableFrom(clrType))
            {
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(null, [modelBuilder]);
            }
        }

        return modelBuilder;
    }
    
    private static readonly string[] LookupNameProperties = ["Name", "UserName"];

    private static void ConfigureEntity(EntityTypeBuilder builder, IMutableEntityType entityType, Type clrType)
    {
        if (typeof(ICreated).IsAssignableFrom(clrType))
        {
            builder.Property(nameof(ICreated.CreatedAt)).IsRequired();
            builder.Property(nameof(ICreated.CreatedBy)).HasMaxLength(128);
            TryHasIndex(builder, entityType, nameof(ICreated.CreatedAt));
        }

        if (typeof(IUpdated).IsAssignableFrom(clrType))
        {
            builder.Property(nameof(IUpdated.UpdatedAt)).HasMaxLength(128);
            TryHasIndex(builder, entityType, nameof(IUpdated.UpdatedAt));
        }

        if (typeof(ISoftDeleted).IsAssignableFrom(clrType))
        {
            builder.Property(nameof(ISoftDeleted.IsDeleted)).IsRequired().HasDefaultValue(false);
            builder.Property(nameof(ISoftDeleted.DeletedBy)).HasMaxLength(128);
            TryHasIndex(builder, entityType, nameof(ISoftDeleted.IsDeleted));
        }

        if (typeof(IHasConcurrencyStamp).IsAssignableFrom(clrType))
        {
            builder.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
                .IsRequired()
                .HasMaxLength(64)
                .IsConcurrencyToken();
        }

        foreach (var name in LookupNameProperties)
        {
            TryHasIndex(builder, entityType, name);
        }
    }

    private static void TryHasIndex(EntityTypeBuilder builder, IMutableEntityType entityType, string propertyName)
    {
        var property = entityType.FindProperty(propertyName);
        if (property is null)
        {
            return;
        }

        var alreadyIndexed = entityType.GetIndexes()
            .Any(index => index.Properties.Count == 1 && index.Properties[0].Name == propertyName);
        if (alreadyIndexed)
        {
            return;
        }

        builder.HasIndex(propertyName);
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeleted
    {
        Expression<Func<TEntity, bool>> filter = entity => !entity.IsDeleted;
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}

public static class ChangeTrackerExtensions
{
    public static IReadOnlyList<TEntity> EntriesOf<TEntity>(
        this Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker tracker,
        params EntityState[] states)
        where TEntity : class =>
        tracker.Entries<TEntity>()
            .Where(entry => states.Length == 0 || states.Contains(entry.State))
            .Select(entry => entry.Entity)
            .ToList();
}
