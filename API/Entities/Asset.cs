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
    public Guid AssetTypeId { get; set; }

    [Required, MaxLength(150)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public AcquisitionType AcquisitionType { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AcquisitionCost { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonthlyLeaseCost { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Available;

    // Εδώ θα αποθηκεύεται όλο το EAV σε JSON μορφή για να μην κάνεις JOINs στα Views
    [Column(TypeName = "jsonb")]
    public string? PropertiesJson { get; set; }

    // Navigation Properties
    // 1 asset έχει 1 τύπο, αλλά 1 τύπος μπορεί να έχει πολλά assets
    [ForeignKey(nameof(AssetTypeId))]
    public  AssetType AssetType { get; set; } = null!;

    // Ένα asset μπορεί να έχει πολλά attribute values (ένα για κάθε πεδίο του τύπου)
    //πχ ένα asset τύπου "Βιβλίο" μπορεί να έχει ένα attribute value για το πεδίο "ISBN", ένα για το "Αριθμός Σελίδων", κλπ.
    public  ICollection<AssetAttributeValue> AttributeValues { get; set; } = new List<AssetAttributeValue>();
    // Ένα asset μπορεί να έχει πολλά  συμβόλαια ενοικιασησ και ενα συμβολαιο μπορει να έχει πολλά  παγια
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<CostAssetHist> MaintenanceHistory { get; set; } = new List<CostAssetHist>();
}
