using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class Contract: BaseEntity
{
    [Required]
    public required Guid CustomerId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public string? Terms { get; set; } // Μπορεί να είναι μεγάλο κείμενο
    public DateTime? SignedDate { get; set; }
    
    [MaxLength(100)]
    public string? AadeNumber { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    public RentalStatus Status { get; set; } = RentalStatus.Pending;

    // Navigation Properties
    
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>(); //ένα Contract μπορεί να περιλαμβάνει πολλά περιουσιακά στοιχεία (Assets)
    
    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;
    public  ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
