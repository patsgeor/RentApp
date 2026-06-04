using API.Entities;
using Humanizer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace API.Data.Contexts;

public class AppDbContext(DbContextOptions options): IdentityDbContext<AppUser>(options)
{


    public DbSet<Rent> Rents { get; set; }
    public DbSet<RentAdjustment> RentAdjustments { get; set; }
    public DbSet<RentAssignment> RentAssignments { get; set; }
    public DbSet<RentHistory> RentHistories { get; set; }
    public DbSet<RentTransfer> RentTransfers { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Depreciation> Depreciations { get; set; }
    public DbSet<DepreciationMethod> DepreciationMethods { get; set; }
    public DbSet<API.Entities.File> Files { get; set; }
    public DbSet<FileType> FileTypes { get; set; }
    public DbSet<FundingAllocation> FundingAllocations { get; set; }
    public DbSet<FundingSource> FundingSources { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Monada> Monades { get; set; }
    public DbSet<UnitMetric> UnitMetrics { get; set; }
    public DbSet<ValuationMethod> ValuationMethods { get; set; }
   
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ⚠️ ΑΥΤΗ Η ΓΡΑΜΜΗ ΕΙΝΑΙ ΤΟ ΚΛΕΙΔΙ! Πρέπει να είναι ΠΡΩΤΗ!
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

         modelBuilder.Entity<IdentityRole>().HasData(// για να προσθέσουμε κάποιους ρόλους στη βάση δεδομένων κατά την δημιουργία της
            new IdentityRole {Id="admin-id",Name = "Admin", NormalizedName = "ADMIN" , ConcurrencyStamp = "1"},
            new IdentityRole {Id="moderator-id", Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "2" },
            new IdentityRole {Id="member-id", Name = "Member", NormalizedName = "MEMBER" , ConcurrencyStamp = "3"},
            new IdentityRole {Id="viewer-id", Name = "Viewer", NormalizedName = "VIEWER" , ConcurrencyStamp = "4"}
        );

        //===========================================================================
        // για sqlite που δεν υποστηρίζει το [Timestamp] attribute, προσθέτουμε default value σε επίπεδο βάσης
        //  για να εξασφαλίσουμε ότι κάθε φορά που εισάγεται ή ενημερώνεται μια εγγραφή,
        //  το πεδίο RowVersion θα λαμβάνει μια νέα τυχαία τιμή, επιτρέποντας έτσι την ανίχνευση συγκρούσεων 
        // κατά την ενημέρωση των εγγραφών.
        //===========================================================================
        // Προσθέτουμε προεπιλεγμένη τιμή σε επίπεδο βάσης για την SQLite
       modelBuilder.Entity<Monada>()
            .Property(m => m.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");

        modelBuilder.Entity<FundingAllocation>()
            .Property(f => f.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");

        modelBuilder.Entity<Invoice>()
            .Property(i => i.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");

        modelBuilder.Entity<InvoiceItem>()
            .Property(i => i.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");

        modelBuilder.Entity<Rent>()
            .Property(a => a.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");

        modelBuilder.Entity<RentAssignment>()
            .Property(a => a.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValueSql("x'0000000000000000'");


        //================================================================================================
        //  Για να διασφαλίσουμε ότι όλα τα DateTime που αποθηκεύονται στη βάση είναι σε μορφή UTC 
        // και ότι όταν ανακτώνται από τη βάση ορίζονται ως UTC, προσθέτουμε έναν ValueConverter
        //  που εφαρμόζεται σε όλες τις ιδιότητες τύπου DateTime και DateTime? σε όλα τα entities.
        //================================================================================================
        // Δημιουργούμε έναν ValueConverter για να μετατρέπουμε τα DateTime σε UTC όταν αποθηκεύονται στη βάση 
        // και να τα ορίζουμε ως UTC όταν ανακτώνται από τη βάση
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.ToUniversalTime(), // Όταν ΠΑΕΙ στη βάση να μετατρέπεται σε UTC
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc) // Όταν ΕΡΧΕΤΑΙ από τη βάση να ορίζεται ως UTC, ωστε να έχει και το 'Z' στο τέλος
            );

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : null, // Όταν ΠΑΕΙ στη βάση να μετατρέπεται σε UTC
                v => v.HasValue  ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null // Όταν ΕΡΧΕΤΑΙ από τη βάση να ορίζεται ως UTC, ωστε να έχει και το 'Z' στο τέλος
            );

        // για κάθε πίνακα και για κάθε ιδιότητα που είναι τύπου DateTime ή DateTime? εφαρμόζουμε τον ValueConverter
        foreach ( var entityType in modelBuilder.Model.GetEntityTypes())
        {
           foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) )
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }
}
