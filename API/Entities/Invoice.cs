using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities;

public class Invoice: BaseEntity
{
    [Required]
    public Guid RentalId { get; set; }

    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = null!;
    
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public bool IsPaid { get; set; } = false;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingBalance { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(RentalId))]
    public virtual Rental Rental { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();


}
