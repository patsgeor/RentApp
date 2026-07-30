using System;
using System.ComponentModel.DataAnnotations;
using static API.Entities.Enums;

namespace API.DTOs.Payment;


public class PaymentListItemDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public TransactionType TransactionType { get; set; }
    public PaymentMatchStatus MatchStatus { get; set; }
    public string? Notes { get; set; }
    public string? Description { get; set; }
    public string? TenantReferenceCode { get; set; }

    // Για εισοδήματα: σύνοψη κατανομής σε συμβόλαια
    public List<string>? ContractReferences { get; set; }
    public string? CustomerName { get; set; }

    // Για εισοδήματα: αναλυτική κατανομή ανά δόση (ώστε το UI να μπορεί να κάνει deallocate)
    public List<PaymentAllocationDto> Allocations { get; set; } = [];

    // Για έξοδα: πάγια που αφορά
    public List<string>? AssetNames { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentAllocationDto
{
    public Guid AllocationId { get; set; }
    public Guid InstallmentId { get; set; }
    public string? ContractReferenceCode { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AllocatedAmount { get; set; }
}


public class IncomeCreateDto
{
    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Το ποσό πρέπει να είναι θετικό")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Χρησιμοποιείται για αυτόματη αντιστοίχιση με ReferenceCode συμβολαίου
    [MaxLength(100)]
    public string? TenantReferenceCode { get; set; }

    // Προαιρετική χειροκίνητη κατανομή σε δόσεις κατά την εισαγωγή
    public List<AllocationItemDto> Allocations { get; set; } = [];
}


public class ExpenseCreateDto
{
    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Το ποσό πρέπει να είναι θετικό")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = null!;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public List<Guid>? AssetIds { get; set; }
}
