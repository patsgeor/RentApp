using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities;

public class Contact : BaseEntity
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required, MaxLength(50)]
    public string LastName { get; set; } = null!;
    
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = null!;
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    public bool CanUseAsset { get; set; } = false;
    
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(CustomerId))]
    public  Customer Customer { get; set; } = null!;
}
