using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class AssetAttributeValue: BaseEntity
{
    [Required]
    public Guid AssetId { get; set; }
    [ForeignKey(nameof(AssetId))]
    public  Asset Asset { get; set; } = null!;

    [Required]
    public Guid AssetTypeFieldId { get; set; }
    [ForeignKey(nameof(AssetTypeFieldId))]
    public  AssetTypeField AssetTypeField { get; set; } = null!;



    // Value Columns (Μόνο ένα από αυτά θα είναι γεμάτο κάθε φορά)
    public string? StringValue { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DecimalValue { get; set; }

    public DateTime? DateValue { get; set; }
    
    public bool? BoolValue { get; set; }
}