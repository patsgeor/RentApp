using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Contexts;

public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ErrorLog> ErrorLogs { get; set; }
}
