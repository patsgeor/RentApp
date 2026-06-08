using System.Linq.Expressions;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Contexts;

public class AppDbContext(  DbContextOptions<AppDbContext> options,
                            ITenantProvider tenantProvider
                        ): IdentityDbContext<AppUser>(options)
{
protected Guid CurrentTenantId => tenantProvider.TenantId; // Ιδανικά αυτό γίνεται inject μέσω κάποιου ITenantService

    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetAttributeValue> AssetAttributeValues { get; set; }
    public DbSet<AssetType> AssetTypes { get; set; }
    public DbSet<AssetTypeField> AssetTypeFields { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<ContractAsset> ContractAssets { get; set; }
    public DbSet<CostAssetHist> CostAssetHists { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<FileAttachment> FileAttachments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Payment> Payments { get; set; }
   public DbSet<Tenant> Tenants { get; set; }
   
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // ⚠️ ΑΥΤΗ Η ΓΡΑΜΜΗ ΕΙΝΑΙ ΤΟ ΚΛΕΙΔΙ! Πρέπει να είναι ΠΡΩΤΗ!
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

         builder.Entity<IdentityRole>().HasData(// για να προσθέσουμε κάποιους ρόλους στη βάση δεδομένων κατά την δημιουργία της
            new IdentityRole {Id="admin-id",Name = "Admin", NormalizedName = "ADMIN" , ConcurrencyStamp = "1"},
            new IdentityRole {Id="moderator-id", Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "2" },
            new IdentityRole {Id="member-id", Name = "Member", NormalizedName = "MEMBER" , ConcurrencyStamp = "3"},
            new IdentityRole {Id="viewer-id", Name = "Viewer", NormalizedName = "VIEWER" , ConcurrencyStamp = "4"}
        );

   // 1. Ρύθμιση 1-προς-1 σχέσης AppUser με Member
        builder.Entity<AppUser>()
            .HasOne(a => a.Member)
            .WithOne(m => m.User)
            .HasForeignKey<Member>(m => m.Id)
            .OnDelete(DeleteBehavior.Cascade);


        // 2. Global Query Filters για Multi-Tenancy & Soft Delete
        // Εφαρμόζουμε δυναμικά σε όσες κλάσεις κάνουν implement το IMustHaveTenant
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                // Φιλτράρισμα βάσει TenantId. Αν είναι και BaseEntity, προσθέτουμε και το Soft Delete.
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

            // 3. PostgreSQL Concurrency: Ρύθμιση xmin ως Concurrency Token για τις BaseEntities
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .IsRowVersion(); // Απαιτεί Npgsql.EntityFrameworkCore.PostgreSQL
            }
        }

        
        // Ρύθμιση της σχέσης Many-to-Many μέσω του ContractAsset
        builder.Entity<ContractAsset>()
            .HasOne(ri => ri.Contract)
            .WithMany(r => r.ContractAssets)
            .HasForeignKey(ri => ri.ContractId)
            // Αν διαγραφεί το Contract (hard delete), σβήνουμε και τις γραμμές του από τον ενδιάμεσο πίνακα
            .OnDelete(DeleteBehavior.Cascade); 

        builder.Entity<ContractAsset>()
            .HasOne(ri => ri.Asset)
            .WithMany(a => a.ContractAssets)
            .HasForeignKey(ri => ri.AssetId)
            // ΔΕΝ επιτρέπουμε να διαγραφεί ένα Asset αν υπάρχει σε ContractAssets (έστω και παλιά)
            .OnDelete(DeleteBehavior.Restrict);

        // Το Customer προς Contract παραμένει Restrict
        builder.Entity<Contract>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Contracts)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ContractAsset>()
            .HasIndex(x => new{x.ContractId,x.AssetId})
            .IsUnique();

        // 1. Το όνομα του Field πρέπει να είναι μοναδικό ΑΝΑ Τύπο Παγίου (και ανά Tenant εννοείται)
        builder.Entity<AssetTypeField>()
            .HasIndex(f => new{f.TenantId,f.AssetTypeId,f.Name})
            .IsUnique();
        
        builder.Entity<Customer>()
            .HasIndex(x => new{x.TenantId,x.Afm})
            .IsUnique();
        
        builder.Entity<AssetType>()
            .HasIndex(x => new{x.TenantId,x.Name})
            .IsUnique();

        // 2. Ένα Πάγιο (Asset) μπορεί να έχει ΜΟΝΟ ΜΙΑ τιμή για κάθε Συγκεκριμένο Πεδίο (Field)
        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => new { av.AssetId, av.AssetTypeFieldId })
            .IsUnique();

        // 3. Ευρετήρια (Indexes) για γρήγορη αναζήτηση στα EAV values
        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.StringValue);
            
        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.DecimalValue);

        builder.Entity<AssetAttributeValue>()
            .HasIndex(av => av.DateValue);

        builder.Entity<Asset>()
            .HasIndex(x => x.TenantId);

        builder.Entity<Contract>()
            .HasIndex(x => x.TenantId);

        builder.Entity<Invoice>()
            .HasIndex(x => x.TenantId);

        builder.Entity<Payment>()
            .HasIndex(x => x.TenantId);

        // 4. Εξασφάλιση ότι το PropertiesJson θα είναι όντως JSONB στην PostgreSQL
        // (Αν χρησιμοποιείς Npgsql)
        builder.Entity<Asset>()
            .Property(a => a.PropertiesJson)
            .HasColumnType("jsonb");
    }

    

    // Helper method για την κατασκευή δυναμικών lambda expressions στα Query Filters
    private LambdaExpression ConvertFilterExpression<TInterface>(
        System.Linq.Expressions.Expression<Func<TInterface, bool>> filterExpression, Type entityType)
    {
        var newParam = System.Linq.Expressions.Expression.Parameter(entityType);
        var newBody = ReplacingExpressionVisitor.Replace(filterExpression.Parameters.Single(), newParam, filterExpression.Body);
        return System.Linq.Expressions.Expression.Lambda(newBody, newParam);
    }
}

// Βοηθητική κλάση για το expression tree 
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
