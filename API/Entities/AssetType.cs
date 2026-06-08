using System.ComponentModel.DataAnnotations;
using API.Interfaces;

namespace API.Entities;

//  Ορίζει το είδος του παγίου (π.χ. "Βιβλίο", "Αποθήκη", "Όχημα").
// Κάθε πελάτης (Tenant) μπορεί να φτιάξει τους δικούς του τύπους.
public class AssetType: IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required Guid TenantId { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; } 

    [MaxLength(250)]
    public string? Description { get; set; }

    // Navigation Properties
    public  ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public  ICollection<AssetTypeField> Fields { get; set; } = new List<AssetTypeField>();

}
