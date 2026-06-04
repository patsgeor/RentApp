using System;
using System.ComponentModel.DataAnnotations;
using API.Interfaces;

namespace API.Entities;

public class AssetCategory: IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    // Navigation Properties
    public  ICollection<Asset> Assets { get; set; } = new List<Asset>();

}
