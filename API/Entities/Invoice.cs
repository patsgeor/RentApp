using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class Invoice : BaseEntity
{
    [Required]
    public Guid ContractId { get; set; }

    // Σειριακός αριθμός οφειλής στο συμβόλαιο (1, 2, 3...)
    public int InstallmentNumber { get; set; }

    // Προαιρετικός αριθμός τιμολογίου (αν εκδίδεται και φορολογικό παραστατικό)
    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime DueDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }       // καθαρό ποσό

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }    // ΦΠΑ

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }  // Amount + TaxAmount

    // Denormalized — ενημερώνεται σε κάθε PaymentAllocation
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; } = 0;

    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(ContractId))]
    public Contract Contract { get; set; } = null!;

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}