using System;
using API.Data.Contexts;
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
    public async Task<IActionResult> GetAll()
    {
        var tenants = await context.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.VatNumber,
                t.ContactInfo,
                t.SubscriptionStatus,
                t.PlanType
            })
            .OrderBy(t => t.Name)
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