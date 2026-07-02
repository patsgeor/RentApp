using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities;
// μια πληρωμή μπορεί να αφορά σε πολλά τιμολόγια (π.χ. πληρωμή για 2 τιμολόγια) και ένα τιμολόγιο μπορεί να έχει πολλές πληρωμές (π.χ. μερική πληρωμή)

public class PaymentAllocation : BaseEntity
{
    [Required]
    public Guid PaymentId { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(PaymentId))]
    public Payment Payment { get; set; } = null!;

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;
}