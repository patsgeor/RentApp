using static API.Entities.Enums;

namespace API.DTOs.Tenant;

public class UpgradeRequestDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }        // μόνο στη cross-tenant λίστα του SuperAdmin
    public PlanType CurrentPlanType { get; set; }
    public PlanType RequestedPlanType { get; set; }
    public UpgradeRequestStatus Status { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public record CreateUpgradeRequestDto(PlanType RequestedPlanType, string? Note);
public record ReviewUpgradeRequestDto(string? ReviewNote);
