using System;
using API.Data.Contexts;
using API.DTOs.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Controllers;

[Authorize(Policy = "SuperAdminRole")]
public class TenantController(AppDbContext context) : BaseApiController
{
    // GET api/tenant
    [HttpGet]
    public async Task<ActionResult<List<TenantAdminDto>>> GetAll()
    {
        var tenants = await context.Tenants
            .IgnoreQueryFilters()
            .OrderBy(t => t.Name)
            .Select(t => new TenantAdminDto
            {
                Id                 = t.Id,
                Name               = t.Name,
                VatNumber          = t.VatNumber,
                ContactInfo        = t.ContactInfo,
                PlanType           = t.PlanType,
                PlanExpiresAt      = t.PlanExpiresAt,
                SubscriptionStatus = t.SubscriptionStatus,
                // AppUser has its own tenant query filter — must be explicitly ignored here,
                // otherwise this subquery is silently scoped to the current (SuperAdmin's) tenant
                // and every other tenant's UserCount comes back as 0.
                UserCount          = context.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id)
            })
            .ToListAsync();

        return Ok(tenants);
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
}

public record UpdatePlanDto(PlanType PlanType);
public record UpdateStatusDto(SubscriptionStatus SubscriptionStatus);