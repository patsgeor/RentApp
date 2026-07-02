using System.ComponentModel.DataAnnotations;
using API.Interfaces;

namespace API.Entities;

public class PaymentAsset : IMustHaveTenant
{
    [Required]
    public Guid PaymentId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public required Guid TenantId { get; set; }

    public Payment Payment { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
