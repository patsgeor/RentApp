using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Contexts;

/// <summary>
/// Δεύτερη, ελαφριά όψη πάνω στους ίδιους πίνακες καταγραφής, ώστε το
/// AuditBackgroundService να γράφει χωρίς να εμπλέκεται με τον change tracker
/// και τα φίλτρα ενοίκου του AppDbContext.
///
/// ΠΡΟΣΟΧΗ: το σχήμα των πινάκων ορίζεται στον AppDbContext, ο οποίος κατέχει
/// και τα migrations. Τυχόν διαμόρφωση (indexes, μήκη στηλών) που δηλωθεί εδώ
/// δεν παράγει migration και δεν εφαρμόζεται ποτέ.
/// </summary>
public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ErrorLog> ErrorLogs { get; set; }
}
