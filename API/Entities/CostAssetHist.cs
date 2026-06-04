using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities;

public class CostAssetHist: BaseEntity
{
    [Required]
    public Guid AssetId { get; set; }

    public DateTime Date { get; set; }
    
    [Required, MaxLength(250)]
    public string Description { get; set; } = null!;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }
    
    [MaxLength(100)]
    public string? MaintainedBy { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(AssetId))]
    public  Asset Asset { get; set; } = null!;
}
