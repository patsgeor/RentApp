using API.Data.Contexts;
using API.DTOs.Tenant;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Controllers;

/// <summary>
/// Ο tenant δηλώνει ενδιαφέρον για αναβάθμιση πλάνου — δεν αλλάζει τίποτα από
/// μόνο του. Η έγκριση γίνεται αποκλειστικά από τον SuperAdmin (βλ. TenantController).
/// </summary>
[Authorize(Policy = "RequireAdminRole")]
public class UpgradeRequestController(AppDbContext context, ITenantProvider tenantProvider) : BaseApiController
{
    // GET api/UpgradeRequest — τα αιτήματα του δικού μου tenant (το global query filter το περιορίζει ήδη)
    [HttpGet]
    public async Task<ActionResult<List<UpgradeRequestDto>>> GetMine()
    {
        var tenant = await context.Tenants.AsNoTracking()
            .FirstAsync(t => t.Id == tenantProvider.TenantId);

        var requests = await context.PlanUpgradeRequests
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests.Select(r => ToDto(r, tenant.Name, tenant.PlanType)).ToList());
    }

    // POST api/UpgradeRequest
    [HttpPost]
    public async Task<ActionResult<UpgradeRequestDto>> Create(CreateUpgradeRequestDto dto)
    {
        try
        {
            var tenant = await context.Tenants.FirstAsync(t => t.Id == tenantProvider.TenantId);

            if (dto.RequestedPlanType <= tenant.PlanType)
                throw new BadRequestException("Το ζητούμενο πλάνο πρέπει να είναι ανώτερο από το τρέχον.");

            var hasPending = await context.PlanUpgradeRequests
                .AnyAsync(r => r.Status == UpgradeRequestStatus.Pending);
            if (hasPending)
                throw new BadRequestException("Υπάρχει ήδη εκκρεμές αίτημα αναβάθμισης.");

            var request = new PlanUpgradeRequest
            {
                TenantId          = tenantProvider.TenantId,
                RequestedPlanType = dto.RequestedPlanType,
                Note              = dto.Note,
                CreatedBy         = User.GetMemberId()
            };

            context.PlanUpgradeRequests.Add(request);
            await context.SaveChangesAsync();

            return Ok(ToDto(request, tenant.Name, tenant.PlanType));
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private static UpgradeRequestDto ToDto(PlanUpgradeRequest r, string? tenantName, PlanType currentPlanType) => new()
    {
        Id                = r.Id,
        TenantId          = r.TenantId,
        TenantName        = tenantName,
        CurrentPlanType   = currentPlanType,
        RequestedPlanType = r.RequestedPlanType,
        Status            = r.Status,
        Note              = r.Note,
        ReviewNote        = r.ReviewNote,
        CreatedAt         = r.CreatedAt,
        ReviewedAt        = r.Status == UpgradeRequestStatus.Pending ? null : r.UpdatedAt
    };
}
