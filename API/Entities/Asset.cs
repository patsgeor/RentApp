using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Entities;

// αντιποσωπεύει ένα περιουσιακό στοιχείο (Rent) που μπορεί να αποσβένεται 
// περιλαμβάνει πληροφορίες για την αξία, την ημερομηνία κτήσης, την ωφέλιμη ζωή, και τις μεθόδους αποσβέσεων και αποτίμησης που ειναι τοποθετημένες στο σύστημα
public class Asset: BaseEntity
{
    [Required]
    public Guid AssetCategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = null!;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    [MaxLength(100)]
    public string? SerialNumber { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    public double? Size { get; set; }
    
    [MaxLength(50)]
    public string? DoorPassword { get; set; }

    public AcquisitionType AcquisitionType { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AcquisitionCost { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonthlyLeaseCost { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Available;

    // Navigation Properties
    [ForeignKey(nameof(AssetCategoryId))]
    public virtual AssetCategory Category { get; set; } = null!;
    public virtual ICollection<RentalAsset> RentalAssets { get; set; } = new List<RentalAsset>();
    public virtual ICollection<CostAssetHist> MaintenanceHistory { get; set; } = new List<CostAssetHist>();
}
