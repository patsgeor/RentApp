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

    // ── Στατιστικά χρήσης (πόσο ενεργός είναι ο tenant) ──────────────
    public int AssetCount { get; set; }
    public int CustomerCount { get; set; }
    public int ContractCount { get; set; }
    public int TransactionCount { get; set; }
    public int FileCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    /// <summary>Άθροισμα βασικών εγγραφών — proxy για τον όγκο δεδομένων.</summary>
    public int TotalRecords { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class TenantUserDto
{
    public string Id { get; set; } = null!;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string TableName { get; set; } = null!;
    public string RecordId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ErrorLogDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? UserId { get; set; }
    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;
    public int StatusCode { get; set; }
    public string Message { get; set; } = null!;
    public string ExceptionType { get; set; } = null!;
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>KPIs για το dashboard του SuperAdmin (όλη η πλατφόρμα).</summary>
public class PlatformSummaryDto
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TrialTenants { get; set; }
    public int SuspendedTenants { get; set; }
    public int TotalUsers { get; set; }
    public int TotalAssets { get; set; }
    public int TotalContracts { get; set; }
    public int TotalCustomers { get; set; }
    public decimal PlatformIncome { get; set; }
    public decimal PlatformExpenses { get; set; }
    public List<PlanBreakdownDto> PlanBreakdown { get; set; } = [];
    public List<TenantAdminDto> TopTenants { get; set; } = [];
}

public class PlanBreakdownDto
{
    public PlanType PlanType { get; set; }
    public int Count { get; set; }
}
