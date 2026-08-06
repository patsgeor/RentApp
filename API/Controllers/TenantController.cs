using System;
using API.Data.Contexts;
using API.DTOs.Tenant;
using API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Controllers;

// ΣΗΜΑΝΤΙΚΟ: κάθε query εδώ είναι cross-tenant, άρα ΠΡΕΠΕΙ να χρησιμοποιεί
// IgnoreQueryFilters() — αλλιώς το global tenant filter το περιορίζει στον
// (System) tenant του SuperAdmin και επιστρέφει μηδενικά.
[Authorize(Policy = "SuperAdminRole")]
public class TenantController(
    AppDbContext context,
    AuditDbContext auditContext,
    UserManager<AppUser> userManager) : BaseApiController
{
    // GET api/tenant
    [HttpGet]
    public async Task<ActionResult<List<TenantAdminDto>>> GetAll()
    {
        var tenants = await context.Tenants
            .IgnoreQueryFilters()
            .OrderBy(t => t.Name)
            .ToListAsync();

        var result = new List<TenantAdminDto>();
        foreach (var t in tenants)
            result.Add(await BuildTenantDtoAsync(t));

        return Ok(result);
    }

    // GET api/tenant/{id}/users
    [HttpGet("{id:guid}/users")]
    public async Task<ActionResult<List<TenantUserDto>>> GetUsers(Guid id)
    {
        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == id)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var result = new List<TenantUserDto>();
        foreach (var u in users)
        {
            result.Add(new TenantUserDto
            {
                Id             = u.Id,
                Email          = u.Email,
                DisplayName    = u.DisplayName,
                IsActive       = u.IsActive,
                EmailConfirmed = u.EmailConfirmed,
                Roles          = [.. await userManager.GetRolesAsync(u)]
            });
        }

        return Ok(result);
    }

    // GET api/tenant/audit-logs?tenantId=&action=&search=&page=1&pageSize=50
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize is < 1 or > 200) pageSize = 50;
        if (page < 1) page = 1;

        var query = auditContext.AuditLogs.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)                     query = query.Where(l => l.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(action))    query = query.Where(l => l.Action == action);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.TableName.ToLower().Contains(s) || l.RecordId.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Τα ονόματα tenant ζουν σε άλλο DbContext — τα ενώνουμε στη μνήμη
        var names = await context.Tenants.IgnoreQueryFilters()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var items = logs.Select(l => new AuditLogDto
        {
            Id         = l.Id,
            TenantId   = l.TenantId,
            TenantName = names.GetValueOrDefault(l.TenantId),
            TableName  = l.TableName,
            RecordId   = l.RecordId,
            Action     = l.Action,
            UserId     = l.UserId,
            Timestamp  = l.Timestamp
        }).ToList();

        return Ok(new
        {
            items,
            metadata = new
            {
                currentPage = page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    // GET api/tenant/error-logs?tenantId=&search=&page=1&pageSize=50
    [HttpGet("error-logs")]
    public async Task<IActionResult> GetErrorLogs(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize is < 1 or > 200) pageSize = 50;
        if (page < 1) page = 1;

        var query = auditContext.ErrorLogs.AsNoTracking().AsQueryable();

        if (tenantId.HasValue) query = query.Where(l => l.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l =>
                l.Message.ToLower().Contains(s) ||
                l.Path.ToLower().Contains(s) ||
                l.ExceptionType.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var names = await context.Tenants.IgnoreQueryFilters()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var items = logs.Select(l => new ErrorLogDto
        {
            Id            = l.Id,
            TenantId      = l.TenantId,
            TenantName    = l.TenantId.HasValue ? names.GetValueOrDefault(l.TenantId.Value) : null,
            UserId        = l.UserId,
            Method        = l.Method,
            Path          = l.Path,
            StatusCode    = l.StatusCode,
            Message       = l.Message,
            ExceptionType = l.ExceptionType,
            StackTrace    = l.StackTrace,
            Timestamp     = l.Timestamp
        }).ToList();

        return Ok(new
        {
            items,
            metadata = new
            {
                currentPage = page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    // GET api/tenant/platform-summary
    [HttpGet("platform-summary")]
    public async Task<ActionResult<PlatformSummaryDto>> GetPlatformSummary()
    {
        var tenants = await context.Tenants.IgnoreQueryFilters().ToListAsync();

        var summary = new PlatformSummaryDto
        {
            TotalTenants     = tenants.Count,
            ActiveTenants    = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Active),
            TrialTenants     = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Trial),
            SuspendedTenants = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Suspended),
            TotalUsers       = await context.Users.IgnoreQueryFilters().CountAsync(),
            TotalAssets      = await context.Assets.IgnoreQueryFilters().Where(a => !a.IsDeleted).CountAsync(),
            TotalContracts   = await context.Contracts.IgnoreQueryFilters().Where(c => !c.IsDeleted).CountAsync(),
            TotalCustomers   = await context.Customers.IgnoreQueryFilters().Where(c => !c.IsDeleted).CountAsync(),
            PlatformIncome   = await context.Payments.IgnoreQueryFilters()
                                    .Where(p => !p.IsDeleted && p.TransactionType == TransactionType.Income)
                                    .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            PlatformExpenses = await context.Payments.IgnoreQueryFilters()
                                    .Where(p => !p.IsDeleted && p.TransactionType == TransactionType.Expense)
                                    .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            PlanBreakdown    = tenants.GroupBy(t => t.PlanType)
                                    .Select(g => new PlanBreakdownDto { PlanType = g.Key, Count = g.Count() })
                                    .OrderBy(p => p.PlanType)
                                    .ToList()
        };

        var all = new List<TenantAdminDto>();
        foreach (var t in tenants)
            all.Add(await BuildTenantDtoAsync(t));

        summary.TopTenants = all.OrderByDescending(t => t.TotalRecords).Take(5).ToList();

        return Ok(summary);
    }

    // PATCH api/tenant/{id}/plan
    [HttpPatch("{id:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePlanDto dto)
    {
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null) return NotFound();

        tenant.PlanType = dto.PlanType;
        await context.SaveChangesAsync();

        return Ok(new { tenant.Id, tenant.Name, tenant.PlanType });
    }

    // PATCH api/tenant/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null) return NotFound();

        tenant.SubscriptionStatus = dto.SubscriptionStatus;
        await context.SaveChangesAsync();

        return Ok(new { tenant.Id, tenant.Name, tenant.SubscriptionStatus });
    }

    // ── helpers ──────────────────────────────────────────────────────
    private async Task<TenantAdminDto> BuildTenantDtoAsync(Entities.Tenant t)
    {
        var assets     = await context.Assets.IgnoreQueryFilters().CountAsync(a => a.TenantId == t.Id && !a.IsDeleted);
        var customers  = await context.Customers.IgnoreQueryFilters().CountAsync(c => c.TenantId == t.Id && !c.IsDeleted);
        var contracts  = await context.Contracts.IgnoreQueryFilters().CountAsync(c => c.TenantId == t.Id && !c.IsDeleted);
        var payments   = await context.Payments.IgnoreQueryFilters().Where(p => p.TenantId == t.Id && !p.IsDeleted).ToListAsync();
        var photos     = await context.Photos.IgnoreQueryFilters().CountAsync(p => p.TenantId == t.Id);
        var files      = await context.FileAttachments.IgnoreQueryFilters().CountAsync(f => f.TenantId == t.Id && !f.IsDeleted);
        var users      = await context.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id);

        var lastPayment  = payments.Count > 0 ? payments.Max(p => p.CreatedAt) : (DateTime?)null;
        var lastContract = await context.Contracts.IgnoreQueryFilters()
            .Where(c => c.TenantId == t.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => (DateTime?)c.CreatedAt)
            .FirstOrDefaultAsync();

        return new TenantAdminDto
        {
            Id                 = t.Id,
            Name               = t.Name,
            VatNumber          = t.VatNumber,
            ContactInfo        = t.ContactInfo,
            PlanType           = t.PlanType,
            PlanExpiresAt      = t.PlanExpiresAt,
            SubscriptionStatus = t.SubscriptionStatus,
            UserCount          = users,
            AssetCount         = assets,
            CustomerCount      = customers,
            ContractCount      = contracts,
            TransactionCount   = payments.Count,
            FileCount          = photos + files,
            TotalIncome        = payments.Where(p => p.TransactionType == TransactionType.Income).Sum(p => p.Amount),
            TotalExpenses      = payments.Where(p => p.TransactionType == TransactionType.Expense).Sum(p => p.Amount),
            TotalRecords       = assets + customers + contracts + payments.Count,
            LastActivity       = new[] { lastPayment, lastContract }.Where(d => d.HasValue).DefaultIfEmpty(null).Max()
        };
    }
}

public record UpdatePlanDto(PlanType PlanType);
public record UpdateStatusDto(SubscriptionStatus SubscriptionStatus);
