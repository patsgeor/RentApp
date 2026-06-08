using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace API.Entities;

public class AppUser:IdentityUser, IMustHaveTenant
{
    [Required]
    public Guid TenantId { get; set; }
    
    [Required, MaxLength(50)]
    public required string DisplayName { get; set; }
    
    [MaxLength(250)]
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public bool IsActive { get; set; } = false;

    // Navigation Properties
    [ForeignKey(nameof(TenantId))]
    public  Tenant Tenant { get; set; } = null!;
    public  Member Member { get; set; } = null!;


}
