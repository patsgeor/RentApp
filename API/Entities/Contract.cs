using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static API.Entities.Enums;

namespace API.Entities;

public class Contract : BaseEntity
{
    [Required]
    public required Guid CustomerId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public string? Terms { get; set; }
    public DateTime? SignedDate { get; set; }

    [MaxLength(100)]
    public string? AadeNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    public RentalStatus Status { get; set; } = RentalStatus.Pending;

    // Μοναδικός κωδικός που χρησιμοποιεί ο ενοικιαστής στις τραπεζικές καταθέσεις
    [MaxLength(50)]
    public string? ReferenceCode { get; set; }

    public InstallmentFrequency InstallmentFrequency { get; set; } = InstallmentFrequency.Monthly;

    // Navigation
    public ICollection<ContractAsset>    ContractAssets   { get; set; } = new List<ContractAsset>();
    public ICollection<Invoice>          Invoices         { get; set; } = new List<Invoice>();
    public ICollection<PaymentContract>  PaymentContracts { get; set; } = new List<PaymentContract>();

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;
}