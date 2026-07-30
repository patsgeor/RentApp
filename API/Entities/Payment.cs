using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;
// Έσοδα: Payment → PaymentInstallment → Installment → Contract (μέσω κατανομής σε δόσεις)
// Έξοδα: Payment → PaymentAsset → Asset
public class Payment : BaseEntity
{
    public DateTime PaymentDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Denormalized: Amount - Sum(Allocations). >0 σημαίνει αδιάθετο/προκαταβολή.
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnallocatedAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public TransactionType TransactionType { get; set; }

    public PaymentMatchStatus MatchStatus { get; set; } = PaymentMatchStatus.Unmatched;

    // Ο κωδικός που έγραψε ο ενοικιαστής στην τραπεζική κατάθεση
    [MaxLength(100)]
    public string? TenantReferenceCode { get; set; }

    // Navigation
    public ICollection<PaymentAsset>       PaymentAssets { get; set; } = new List<PaymentAsset>();
    public ICollection<PaymentInstallment> Allocations   { get; set; } = new List<PaymentInstallment>();
}