// API/DTOs/Payment/InstallmentDto.cs
using static API.Entities.Enums;

namespace API.DTOs.Payment;

public class InstallmentDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int InstallmentNumber { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal OutstandingAmount => TotalAmount - AllocatedAmount;
    public InstallmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public List<AllocationSummaryDto> Allocations { get; set; } = [];
}

public class AllocationSummaryDto
{
    public Guid AllocationId { get; set; }
    public Guid PaymentId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal AllocatedAmount { get; set; }
}

public class AllocationItemDto
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class MatchResultDto
{
    public bool Matched { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractReferenceCode { get; set; }
    public decimal TotalAllocated { get; set; }
    public decimal Unallocated { get; set; }
    public List<AllocationSummaryDto> Allocations { get; set; } = [];
    public string? Message { get; set; }
}