using System;
using System.ComponentModel.DataAnnotations;

namespace API.Entities;

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid TenantId { get; set; } // Κρίσιμο και εδώ για απομόνωση

    [Required, MaxLength(100)]
    public string TableName { get; set; } = null!;
    
    [Required, MaxLength(100)]
    public string RecordId { get; set; } = null!; // String γιατί μπορεί να είναι Guid ή string (π.χ. Identity)
    
    [Required, MaxLength(20)]
    public string Action { get; set; } = null!; // Insert, Update, Delete
    
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    
    [MaxLength(50)]
    public string? UserId { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
