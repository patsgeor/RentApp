using System.Linq.Expressions;
using System.Text.Json;
using API.Entities;
using API.Interfaces;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Contexts;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantProvider tenantProvider,
    AuditChannel auditChannel
) : IdentityDbContext<AppUser>(options)
{
    protected Guid CurrentTenantId => tenantProvider.TenantId;

    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetAttributeValue> AssetAttributeValues { get; set; }
    public DbSet<AssetType> AssetTypes { get; set; }
    public DbSet<AssetTypeField> AssetTypeFields { get; set; }
    public DbSet<AssetTypeFieldOption> AssetTypeFieldOptions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<ContractAsset> ContractAssets { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<FileAttachment> FileAttachments { get; set; }
    public DbSet<Photo> Photos { get; set; }
    // public DbSet<Installment>   Installments {get; set;}
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<MemberInvite> MemberInvites { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentAsset> PaymentAssets { get; set; }
    public DbSet<PaymentContract> PaymentContracts { get; set; }
    public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "super-admin-id", Name = "SuperAdmin", NormalizedName = "SUPERADMIN", ConcurrencyStamp = "0" },
            new IdentityRole { Id = "admin-id", Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "1" },
            new IdentityRole { Id = "moderator-id", Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "2" },
            new IdentityRole { Id = "member-id", Name = "Member", NormalizedName = "MEMBER", ConcurrencyStamp = "3" },
            new IdentityRole { Id = "viewer-id", Name = "Viewer", NormalizedName = "VIEWER", ConcurrencyStamp = "4" }
        );

        builder.Entity<AppUser>()
            .HasOne(a => a.Member)
            .WithOne(m => m.User)
            .HasForeignKey<Member>(m => m.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Global Query Filters: Multi-Tenancy & Soft Delete
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    builder.Entity(entityType.ClrType)
                        .HasQueryFilter(ConvertFilterExpression<IMustHaveTenant>(e =>
                            e.TenantId == CurrentTenantId && !((BaseEntity)e).IsDeleted, entityType.ClrType));
                }
                else
                {
                    builder.Entity(entityType.ClrType)
                        .HasQueryFilter(ConvertFilterExpression<IMustHaveTenant>(e =>
                            e.TenantId == CurrentTenantId, entityType.ClrType));
                }
            }

            // PostgreSQL Concurrency: xmin as Concurrency Token for BaseEntities
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .IsRowVersion();
            }
        }

        // ── ContractAsset ───────────────────────────────────────────────
        builder.Entity<ContractAsset>()
            .HasOne(ca => ca.Contract)
            .WithMany(c => c.ContractAssets)
            .HasForeignKey(ca => ca.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<ContractAsset>()
            .HasOne(ca => ca.Asset)
            .WithMany(a => a.ContractAssets)
            .HasForeignKey(ca => ca.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ContractAsset>()
            .HasIndex(x => new { x.ContractId, x.AssetId })
            .IsUnique();

        // ── Contract ────────────────────────────────────────────────────
        builder.Entity<Contract>()
            .HasOne(c => c.Customer)
            .WithMany(cu => cu.Contracts)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ReferenceCode unique per tenant (filtered: only non-null values)
        builder.Entity<Contract>()
            .HasIndex(c => new { c.TenantId, c.ReferenceCode })
            .IsUnique()
            .HasFilter("\"ReferenceCode\" IS NOT NULL");

        builder.Entity<Contract>()
            .HasIndex(c => c.TenantId);

        // ── PaymentContract (many-to-many, no tenant filter) ─────────────
        builder.Entity<PaymentContract>()
            .HasKey(pc => new { pc.PaymentId, pc.ContractId });

        builder.Entity<PaymentContract>()
            .HasOne(pc => pc.Payment)
            .WithMany(p => p.PaymentContracts)
            .HasForeignKey(pc => pc.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentContract>()
            .HasOne(pc => pc.Contract)
            .WithMany(c => c.PaymentContracts)
            .HasForeignKey(pc => pc.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── PaymentAllocation ───────────────────────────────────────────
        builder.Entity<PaymentAllocation>()
            .HasOne(pa => pa.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAllocation>()
            .HasOne(pa => pa.Invoice)
            .WithMany(i => i.Allocations)
            .HasForeignKey(pa => pa.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PaymentAsset ────────────────────────────────────────────────
        builder.Entity<PaymentAsset>()
            .HasKey(pa => new { pa.PaymentId, pa.AssetId });

        builder.Entity<PaymentAsset>()
            .HasOne(pa => pa.Payment)
            .WithMany(p => p.PaymentAssets)
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAsset>()
            .HasOne(pa => pa.Asset)
            .WithMany()
            .HasForeignKey(pa => pa.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Invoice (Installment) ───────────────────────────────────────
        builder.Entity<Invoice>()
            .HasIndex(i => new { i.ContractId, i.InstallmentNumber })
            .IsUnique();

        builder.Entity<Invoice>()
            .HasIndex(i => i.TenantId);

        // ── Payment ─────────────────────────────────────────────────────
        builder.Entity<Payment>()
            .HasIndex(p => p.TenantId);

        builder.Entity<Payment>()
            .HasIndex(p => p.MatchStatus);

        // ── AssetTypeField ───────────────────────────────────────────────
        builder.Entity<AssetTypeField>()
            .HasIndex(f => new { f.TenantId, f.AssetTypeId, f.Name })
            .IsUnique();

        // ── Customer ─────────────────────────────────────────────────────
        builder.Entity<Customer>()
            .HasIndex(x => new { x.TenantId, x.Afm })
            .IsUnique();

        // ── AssetType ────────────────────────────────────────────────────
        builder.Entity<AssetType>()
            .HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique();

        // ── AssetAttributeValue ──────────────────────────────────────────
        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => new { av.AssetId, av.AssetTypeFieldId })
            .IsUnique();

        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.StringValue);

        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.DecimalValue);

        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.DateValue);

        // ── Asset ────────────────────────────────────────────────────────
        builder.Entity<Asset>()
            .HasIndex(a => a.TenantId);

        builder.Entity<Asset>()
            .Property(a => a.PropertiesJson)
            .HasColumnType("jsonb");

        builder.Entity<Asset>()
            .HasIndex(a => new { a.TenantId, a.AssetTypeId })
            .HasDatabaseName("idx_asset_tenant_type");

        builder.Entity<Asset>()
            .HasIndex(a => a.PropertiesJson, "idx_asset_properties_jsonb")
            .HasMethod("GIN");

        // ── Photo ────────────────────────────────────────────────────────
        builder.Entity<Photo>()
            .HasOne(p => p.Asset)
            .WithMany(a => a.Photos)
            .HasForeignKey(p => p.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Photo>()
            .HasIndex(p => p.AssetId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Npgsql requires DateTimeKind.Utc for timestamp with time zone
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var prop in entry.Properties
                .Where(p => p.CurrentValue is DateTime { Kind: DateTimeKind.Unspecified }))
            {
                prop.CurrentValue = DateTime.SpecifyKind((DateTime)prop.CurrentValue!, DateTimeKind.Utc);
            }
        }

        var logs = BuildAuditEntries();
        var result = await base.SaveChangesAsync(cancellationToken);
        foreach (var log in logs)
            auditChannel.Writer.TryWrite(log);
        return result;
    }

    private List<AuditLog> BuildAuditEntries()
    {
        var logs = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var action = entry.State switch
            {
                EntityState.Added    => "Insert",
                EntityState.Modified => "Update",
                _                    => "Delete"
            };

            var userId = entry.State switch
            {
                EntityState.Added    => entry.Entity.CreatedBy,
                EntityState.Modified => entry.Entity.UpdatedBy,
                _                    => entry.Entity.DeletedBy
            };

            var oldValues = entry.State != EntityState.Added
                ? SerializeProps(entry.OriginalValues)
                : null;

            var newValues = entry.State != EntityState.Deleted
                ? SerializeProps(entry.CurrentValues)
                : null;

            logs.Add(new AuditLog
            {
                TenantId  = entry.Entity.TenantId,
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                RecordId  = entry.Entity.Id.ToString(),
                Action    = action,
                OldValues = oldValues,
                NewValues = newValues,
                UserId    = userId,
            });
        }

        return logs;
    }

    private static string SerializeProps(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        var dict = values.Properties
            .ToDictionary(p => p.Name, p => values[p]?.ToString());
        return JsonSerializer.Serialize(dict);
    }

    private LambdaExpression ConvertFilterExpression<TInterface>(
        System.Linq.Expressions.Expression<Func<TInterface, bool>> filterExpression, Type entityType)
    {
        var newParam = System.Linq.Expressions.Expression.Parameter(entityType);
        var newBody = ReplacingExpressionVisitor.Replace(filterExpression.Parameters.Single(), newParam, filterExpression.Body);
        return System.Linq.Expressions.Expression.Lambda(newBody, newParam);
    }
}

internal class ReplacingExpressionVisitor : System.Linq.Expressions.ExpressionVisitor
{
    private readonly System.Linq.Expressions.Expression _oldValue;
    private readonly System.Linq.Expressions.Expression _newValue;

    public ReplacingExpressionVisitor(System.Linq.Expressions.Expression oldValue, System.Linq.Expressions.Expression newValue)
    {
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override System.Linq.Expressions.Expression Visit(System.Linq.Expressions.Expression? node)
    {
        if (node == _oldValue) return _newValue;
        return base.Visit(node)!;
    }

    public static System.Linq.Expressions.Expression Replace(System.Linq.Expressions.Expression oldValue, System.Linq.Expressions.Expression newValue, System.Linq.Expressions.Expression expression)
    {
        return new ReplacingExpressionVisitor(oldValue, newValue).Visit(expression);
    }
}