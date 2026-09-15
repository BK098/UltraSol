using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : ModuleDbContext<OrganizationDbContext>(options), ITransactionPreparation, IRepositoryWritePolicy
{
    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    public Task PrepareTransactionAsync(CancellationToken ct) => Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731011, 1)", ct);
    public void EnsureDeleteAllowed(Type entityType)
    {
        if (entityType == typeof(Department) || entityType == typeof(Employee))
        {
            throw new InvalidOperationException("Use Organization lifecycle commands.");
        }
    }
    public void EnsureBulkWriteAllowed(Type entityType) => EnsureDeleteAllowed(entityType);
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("organization");
        builder.MapMailbox();

        ModelConfigure.Root<Employee>(builder, "employees");
        builder.Entity<Employee>().Property(value => value.Email).HasMaxLength(256);
        builder.Entity<Employee>().Property(value => value.SyncStatus).HasMaxLength(32);
        builder.Entity<Employee>().Property(value => value.SyncError).HasMaxLength(512);
        builder.Entity<Employee>().ToTable("employees").HasKey(value => value.Id);
        builder.Entity<Employee>().HasIndex(value => value.UserId).IsUnique();
        builder.Entity<Employee>().Property<uint>("xmin").IsRowVersion();
        //builder.Entity<Employee>().HasOne<Department>().WithMany().HasForeignKey(value => value.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Employee>()
        .HasOne<Department>()
        .WithMany(department => department.Employees)
        .HasForeignKey(employee => employee.DepartmentId)
        .OnDelete(DeleteBehavior.Restrict);
        ModelConfigure.Root<Department>(builder, "departments");
        builder.Entity<Department>().ToTable("departments").HasKey(value => value.Id);
        builder.Entity<Department>().Property(value => value.Name).HasMaxLength(256);
        builder.Entity<Department>().Property<uint>("xmin").IsRowVersion();
        builder.Entity<Department>()
        .HasOne<Department>()
        .WithMany(department => department.Children)
        .HasForeignKey(department => department.ParentId)
        .OnDelete(DeleteBehavior.Restrict);
        //builder.Entity<Department>().HasOne<Department>().WithMany().HasForeignKey(value => value.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}