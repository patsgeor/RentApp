using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class Payment: BaseEntity
{
    [Required]
    public Guid InvoiceId { get; set; }

    public DateTime PaymentDate { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public TransactionType TransactionType { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(InvoiceId))]
    public virtual Invoice Invoice { get; set; } = null!;
}
