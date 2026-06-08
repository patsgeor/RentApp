using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class Customer : BaseEntity
{
    public CustomerType Type { get; set; }
    
    [Required, MaxLength(150)]
    public required string Name { get; set; }  // Ή Ονοματεπώνυμο
    
    [MaxLength(20)]
    public required string Afm { get; set; }
    
    [MaxLength(50)]
    public string? Dou { get; set; }
    
    [MaxLength(100)]
    public string? Phones { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? Representative { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(TenantId))]
    public  Tenant Tenant { get; set; } = null!;
    public  ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public  ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

