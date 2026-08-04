using static API.Entities.Enums;

namespace API.DTOs.Tenant;

public class TenantAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? VatNumber { get; set; }
    public string? ContactInfo { get; set; }
    public PlanType PlanType { get; set; }
    public DateTime? PlanExpiresAt { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; }
    public int UserCount { get; set; }
}
