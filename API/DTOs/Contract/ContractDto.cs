using API.Helper;
using static API.Entities.Enums;

namespace API.DTOs.Contract;

public class ContractListItemDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? ReferenceCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public RentalStatus Status { get; set; }
    public List<string> AssetNames { get; set; } = new();
}

public class ContractDetailDto
{
    public Guid Id { get; set; }
    public uint RowVersion { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? ReferenceCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public RentalStatus Status { get; set; }
    public InstallmentFrequency InstallmentFrequency { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public List<ContractAssetDto> Assets { get; set; } = new();
}

public class ContractAssetDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal UnitCost { get; set; }
    public RateUnit RateUnit { get; set; }
    public decimal CalculatedAmount { get; set; }
    public string? Notes { get; set; }
}

public class ContractCreateDto
{
    public required Guid CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string? ReferenceCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public InstallmentFrequency InstallmentFrequency { get; set; } = InstallmentFrequency.Monthly;
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public List<ContractAssetCreateDto> Assets { get; set; } = new();
}

public class ContractAssetCreateDto
{
    public Guid AssetId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal UnitCost { get; set; }
    public RateUnit RateUnit { get; set; }
    public decimal CalculatedAmount { get; set; }
    public string? Notes { get; set; }
}

public class ContractUpdateDto
{
    public uint RowVersion { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string? ReferenceCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public RentalStatus Status { get; set; }
    public InstallmentFrequency InstallmentFrequency { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public List<ContractAssetCreateDto> Assets { get; set; } = new();
}

public class AvailableAssetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? AssetTypeName { get; set; }
    public decimal Cost { get; set; }
    public RateUnit RateUnit { get; set; }
}

public class ContractParams : PagingParams
{
    public RentalStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
}
