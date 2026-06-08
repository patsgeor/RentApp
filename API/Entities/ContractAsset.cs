using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities;

public class ContractAsset: BaseEntity
{
    [Required]
    public Guid ContractId { get; set; }
    
    [Required]
    public Guid AssetId { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(ContractId))]
    public  Contract Contract { get; set; } = null!;
    
    [ForeignKey(nameof(AssetId))]
    public  Asset Asset { get; set; } = null!;

}
