using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;
// μια πληρωμή μπορεί να αφορά πολλά συμβόλαια contracts για έσοδα  και ένα συμβόλαιο μπορεί να έχει πολλές πληρωμές (π.χ. ενοίκιο κάθε μήνα) 
// για αυτο έχω τον πινακα PaymentContract 
//  μια πληρωμή μπορεί να αφορά σε πολλά assets  για έξοδα (π.χ. πληρωμή για επισκευή σε πολλά assets) και ένα asset μπορεί να έχει πολλές πληρωμές (π.χ. επισκευές σε διαφορετικά χρονικά διαστήματα)
// για αυτο έχω τον πινακα PaymentAsset
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
    public ICollection<PaymentContract>   PaymentContracts { get; set; } = new List<PaymentContract>();
    public ICollection<PaymentAsset>      PaymentAssets    { get; set; } = new List<PaymentAsset>();
    public ICollection<PaymentAllocation> Allocations      { get; set; } = new List<PaymentAllocation>();
}