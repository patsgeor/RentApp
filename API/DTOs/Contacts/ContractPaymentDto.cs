using System.ComponentModel.DataAnnotations;
using API.Helper;
using static API.Entities.Enums;

namespace API.DTOs.Payment;

public class ContractPaymentDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public RentalStatus Status { get; set; }
    public bool CanExtend { get; set; }
    public List<string> AssetNames { get; set; } = [];
    public string? AadeNumber { get; set; }
    public string? ReferenceCode { get; set; }
}



public class IncomeCreateDto
{
    [Required]
    public Guid ContractId { get; set; }

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Το ποσό πρέπει να είναι θετικό")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
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


public class PaymentListItemDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public TransactionType TransactionType { get; set; }
    public string? Notes { get; set; }
    public string? Description { get; set; }
    public List<Guid>? ContractIds { get; set; }
    public List<string>? CustomerNames { get; set; }
    public List<string>? AssetNames { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public DateTime CreatedAt { get; set; }
}
