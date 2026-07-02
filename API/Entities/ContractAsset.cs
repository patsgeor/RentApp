using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Interfaces;
using static API.Entities.Enums;

namespace API.Entities;

public class ContractAsset 
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ContractId { get; set; }
    
    [Required]
    public Guid AssetId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }       // snapshot Asset.Cost κατά υπογραφή

    public RateUnit RateUnit { get; set; }       // snapshot Asset.RateUnit κατά υπογραφή

    [Column(TypeName = "decimal(18,2)")]
    public decimal CalculatedAmount { get; set; } 

    [MaxLength(500)]
    public string? Notes { get; set; }

    


    // Navigation Properties
    [ForeignKey(nameof(ContractId))]
    public  Contract Contract { get; set; } = null!;
    
    [ForeignKey(nameof(AssetId))]
    public  Asset Asset { get; set; } = null!;
    
}
