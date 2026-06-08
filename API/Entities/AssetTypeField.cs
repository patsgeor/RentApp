using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

// AssetTypeField (Τα δυναμικά πεδία του τύπου)
//  Ορίζει ποια πεδία ανήκουν σε κάθε τύπο (Το "Schema" του παγίου).
// Για παράδειγμα, ο τύπος "Βιβλίο" μπορεί να έχει πεδία "ISBN" (Text), "Αριθμός Σελίδων" (Number), και "Ημερομηνία Έκδοσης" (Date).
public class AssetTypeField: BaseEntity
{
    [Required]
    public Guid AssetTypeId { get; set; }
    [ForeignKey(nameof(AssetTypeId))]
    public virtual AssetType AssetType { get; set; } = null!;

    [Required, MaxLength(100)]
    public required string Name { get; set; }  // Εσωτερικό κλειδί (π.χ. "isbn", "ceiling_height")

    [Required, MaxLength(100)]
    public required string Label { get; set; } // Για το UI (π.χ. "ISBN Βιβλίου", "Ύψος Οροφής")

    public FieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    // Navigation Properties
    public  ICollection<AssetAttributeValue> AttributeValues { get; set; } = new List<AssetAttributeValue>();
}