using UltraSol.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Auth.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : IdentityUserContext<UserAccount, Guid>(options), ITransactionPreparation, IRepositoryWritePolicy
{
    private Guid? _unitOfWorkTransactionId;

    // Keep runtime and design-time models identical; passkeys are outside this foundation.
    protected override Version SchemaVersion => IdentitySchemaVersions.Version2;

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<DirectPermission> DirectPermissions => Set<DirectPermission>();
    public DbSet<AuthorizationVersion> AuthorizationVersions => Set<AuthorizationVersion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema.Name);
        builder.MapMailbox();
        builder.Entity<EmployeeAccount>().ToTable("employee_accounts").HasKey(value => value.EmployeeId);
        builder.Entity<EmployeeAccount>().HasIndex(value => value.UserId).IsUnique();

        var user = builder.Entity<UserAccount>();
        user.ToTable("user_accounts", table =>
        {
            table.HasCheckConstraint("ck_user_accounts_status", "\"Status\" IN (0, 1, 2)");
            table.HasCheckConstraint("ck_user_accounts_deleted_at", "(\"Status\" = 2 AND \"DeletedAt\" IS NOT NULL AND \"DeletedAt\" >= \"CreatedAt\") OR (\"Status\" <> 2 AND \"DeletedAt\" IS NULL)");
        });
        user.Ignore(account => account.DomainEvents);
        user.Property(account => account.Id).ValueGeneratedNever();
        user.Property(account => account.Email).IsRequired();
        user.Property(account => account.UserName).IsRequired();
        user.Property(account => account.NormalizedEmail).IsRequired();
        user.Property(account => account.NormalizedUserName).IsRequired();
        user.Property(account => account.PhoneNumber).HasMaxLength(256);
        user.Property(account => account.CreatedAt).IsRequired();
        user.HasQueryFilter(account => account.Status != AccountStatus.Deleted);
        user.HasIndex(account => account.NormalizedEmail).IsUnique().HasFilter("\"Status\" <> 2");
        user.HasIndex(account => account.NormalizedUserName).IsUnique().HasFilter("\"Status\" <> 2");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityUserLogin<Guid>>().Property(login => login.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<Guid>>().Property(login => login.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<Guid>>().Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<Guid>>().Property(token => token.Name).HasMaxLength(128);

        var session = ModelConfigure.Root<Session>(builder, "sessions");
        session.ToTable("sessions", table =>
        {
            table.HasCheckConstraint("ck_sessions_expiry", "\"ExpiresAt\" > \"CreatedAt\" AND \"LastSeenAt\" >= \"CreatedAt\" AND \"LastSeenAt\" < \"ExpiresAt\"");
            table.HasCheckConstraint("ck_sessions_revocation", "(\"RevokedAt\" IS NULL AND \"RevokeReason\" IS NULL) OR (\"RevokedAt\" IS NOT NULL AND \"RevokedAt\" >= \"LastSeenAt\" AND length(btrim(\"RevokeReason\")) > 0 AND \"RevokeReason\" IS NOT NULL)");
        });
        session.Property(value => value.RevokeReason).HasMaxLength(256);
        session.HasOne<UserAccount>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Restrict);
        session.HasIndex(value => new { value.UserId, value.RevokedAt, value.ExpiresAt });
        session.OwnsOne(value => value.Device, device =>
        {
            device.Property(value => value.Name).HasColumnName("DeviceName").HasMaxLength(128).IsRequired();
            device.Property(value => value.UserAgent).HasColumnName("UserAgent").HasMaxLength(1024);
            device.Property(value => value.IpAddress).HasColumnName("IpAddress").HasMaxLength(64);
        });
        session.Navigation(value => value.Device).IsRequired();
        ConfigureAuthorization(builder);
    }

    private static void ConfigureAuthorization(ModelBuilder builder)
    {
        builder.Entity<Role>().ToTable("roles").HasKey(value => value.Id);
        builder.Entity<Role>().HasIndex(value => value.Code).IsUnique();
        builder.Entity<Role>().Property(value => value.Code).HasMaxLength(128);
        builder.Entity<Role>().Property(value => value.Name).HasMaxLength(256);
        builder.Entity<PermissionDefinition>().ToTable("permissions").HasKey(value => value.Code);
        builder.Entity<PermissionDefinition>().Property(value => value.Code).HasMaxLength(256);
        builder.Entity<RolePermission>().ToTable("role_permissions").HasKey(value => new { value.RoleId, value.PermissionCode });
        builder.Entity<RolePermission>().HasOne<Role>().WithMany().HasForeignKey(value => value.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RolePermission>().HasOne<PermissionDefinition>().WithMany().HasForeignKey(value => value.PermissionCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RoleAssignment>().ToTable("role_assignments").HasKey(value => value.Id);
        builder.Entity<RoleAssignment>().HasIndex(value => value.UserId);
        builder.Entity<RoleAssignment>().HasOne<Role>().WithMany().HasForeignKey(value => value.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RoleAssignment>().HasOne<UserAccount>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DirectPermission>().ToTable("direct_permissions").HasKey(value => value.Id);
        builder.Entity<DirectPermission>().HasIndex(value => value.UserId);
        builder.Entity<DirectPermission>().HasOne<UserAccount>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DirectPermission>().HasOne<PermissionDefinition>().WithMany().HasForeignKey(value => value.PermissionCode).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuthorizationVersion>().ToTable("authorization_version").HasKey(value => value.Id);
        builder.Entity<AuthorizationVersion>().HasData(new AuthorizationVersion());
        builder.Entity<RefreshToken>().ToTable("refresh_tokens").HasKey(value => value.Id);
        builder.Entity<RefreshToken>().Property(value => value.Hash).HasMaxLength(64);
        builder.Entity<RefreshToken>().HasIndex(value => value.Hash).IsUnique();
        builder.Entity<RefreshToken>().HasOne<Session>().WithMany().HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Restrict);
    }

    public Task PrepareTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _unitOfWorkTransactionId = Database.CurrentTransaction?.TransactionId
            ?? throw new InvalidOperationException("Auth requires an active transaction.");
        return Task.CompletedTask;
    }

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use Auth UnitOfWork and SaveChangesAsync.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_unitOfWorkTransactionId is null || Database.CurrentTransaction?.TransactionId != _unitOfWorkTransactionId)
        {
            throw new InvalidOperationException("Run Auth writes inside IAuthUnitOfWork.ExecuteInTransactionAsync or BeginTransactionAsync.");
        }
        if (!acceptAllChangesOnSuccess)
        {
            throw new NotSupportedException("Auth persistence requires acceptAllChangesOnSuccess.");
        }
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted)
            {
                EnsureDeleteAllowed(entry.Entity.GetType());
            }
        }
        var permissionsChanged = ChangeTracker.Entries().Any(entry =>
            (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            (entry.Entity is Role or PermissionDefinition or RolePermission or RoleAssignment or DirectPermission or EmployeeAccount ||
                entry.Entity is UserAccount && (entry.State != EntityState.Modified || entry.Property(nameof(UserAccount.Status)).IsModified)));
        if (permissionsChanged)
        {
            var affected = await Database.ExecuteSqlRawAsync("""UPDATE auth.authorization_version SET "Version" = "Version" + 1 WHERE "Id" = 1""", cancellationToken);
            if (affected != 1)
            {
                throw new InvalidOperationException("Authorization version is missing.");
            }
        }
        return await base.SaveChangesAsync(true, cancellationToken);
    }

    public void EnsureDeleteAllowed(Type entityType)
    {
        if (entityType == typeof(UserAccount) || entityType == typeof(Session))
        {
            throw new InvalidOperationException("Use account SoftDelete or session Revoke instead of deletion.");
        }
    }

    public void EnsureBulkWriteAllowed(Type entityType)
    {
        if (entityType == typeof(UserAccount) || entityType == typeof(Session))
        {
            throw new InvalidOperationException("Auth aggregate changes require tracked entities and their lifecycle methods.");
        }
    }
}