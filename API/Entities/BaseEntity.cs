using System;
using System.ComponentModel.DataAnnotations;
using API.Interfaces;

namespace API.Entities;

public abstract class BaseEntity : IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }
    
    [MaxLength(50)]
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    [MaxLength(50)]
    public string? DeletedBy { get; set; }

    // Θα ρυθμιστεί μέσω Fluent API για το xmin της PostgreSQL
     public uint xmin { get; private set; }
}
