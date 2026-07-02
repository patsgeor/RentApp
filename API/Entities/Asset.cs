using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    
    public RateUnit RateUnit { get; set; } = RateUnit.PerDay;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }


    public AssetStatus Status { get; set; } = AssetStatus.Available;

    public string? PhotoUrl { get; set; }

    // Εδώ θα αποθηκεύεται όλο το EAV σε JSON μορφή για να μην κάνεις JOINs στα Views
    public JsonDocument? PropertiesJson { get; set; }

    // Navigation Properties
    // 1 asset έχει 1 τύπο πχ βιβλιο ή εργαλειο, αλλά 1 τύπος μπορεί να έχει πολλά assets
    [ForeignKey(nameof(AssetTypeId))]
    [JsonIgnore]
    public  AssetType AssetType { get; set; } = null!;

    // Ένα asset μπορεί να έχει πολλά attribute values (ένα για κάθε πεδίο του τύπου)
    //πχ ένα asset τύπου "Βιβλίο" μπορεί να έχει ένα attribute value για το πεδίο "ISBN", ένα για το "Αριθμός Σελίδων", κλπ.
    [JsonIgnore]
    public  ICollection<AssetAttributeValue> AttributeValues { get; set; } = new List<AssetAttributeValue>();
    // Ένα asset μπορεί να έχει πολλά  συμβόλαια ενοικιασησ και ενα συμβολαιο μπορει να έχει πολλά  παγια
    [JsonIgnore]
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    
    
    [JsonIgnore]
    public List<Photo> Photos { get; set; } = new List<Photo>();
}
