using System.ComponentModel.DataAnnotations;

namespace API.Entities;

/// <summary>Εξαιρέσεις που πιάνει το ExceptionMiddleware — ζει στο AuditDbContext
/// γιατί είναι cross-cutting infrastructure logging, όχι δεδομένα tenant.</summary>
public class ErrorLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null όταν το σφάλμα συνέβη πριν αναγνωριστεί ο tenant (π.χ. login).</summary>
    public Guid? TenantId { get; set; }

    [MaxLength(50)]
    public string? UserId { get; set; }

    [Required, MaxLength(10)]
    public string Method { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Path { get; set; } = null!;

    public int StatusCode { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = null!;

    [Required, MaxLength(200)]
    public string ExceptionType { get; set; } = null!;

    public string? StackTrace { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
