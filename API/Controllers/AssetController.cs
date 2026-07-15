using API.Data.Contexts;
using API.DTOs;
using API.DTOs.Asset;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static API.Entities.Enums;

namespace API.Controllers;


[Authorize]
public class AssetController(IAssetService assetService) : BaseApiController
{
    // GET api/asset?page=1&pageSize=20&search=&assetTypeId=&status=
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagingParams pagingParams,
        [FromQuery] Guid? assetTypeId,
        [FromQuery] AssetStatus? status)
    {
        var result = await assetService.GetAllAsync(pagingParams, assetTypeId, status);
        return Ok(result);
    }

    // GET api/asset/lookup?search=&assetTypeId=
    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup([FromQuery] string? search, [FromQuery] Guid? assetTypeId)
    {
        var result = await assetService.GetLookupAsync(search, assetTypeId);
        return Ok(result);
    }

    // GET api/asset/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDetailDto>> GetById(Guid id)
    {
        var asset = await assetService.GetByIdAsync(id);
        return asset == null ? NotFound() : Ok(asset);
    }

    // POST api/asset
    [HttpPost]
    public async Task<ActionResult<AssetDto>> Create(AssetCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await assetService.CreateAsync(dto, User.GetMemberId().ToString());
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/asset/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetDto>> Update(Guid id, AssetUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await assetService.UpdateAsync(id, dto, User.GetMemberId().ToString());
            return Ok(updated);
        }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (NotFoundException ex){ return NotFound(new { message = ex.Message });}
        catch (BadRequestException ex) {return BadRequest(new { message = ex.Message });}
    }

    // PATCH api/asset/{id}/attribute
    // Changes ONE EAV value (e.g. just "color") without resending the
    // whole Attributes set — unlike PUT above, which replaces all of them.
    [HttpPatch("{id:guid}/attribute")]
    public async Task<ActionResult<AssetDto>> UpdateAttribute(Guid id, AssetAttributeUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await assetService.UpdateAttributeAsync(id, dto, User.GetMemberId().ToString());
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PATCH api/asset/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AssetDto>> UpdateStatus(Guid id, AssetStatusUpdateDto dto)
    {
        try
        {
            var updated = await assetService.UpdateStatusAsync(id, dto, User.GetMemberId().ToString());
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }       
        catch (ConflictException ex)    { return Conflict(new { message = ex.Message }); }

    }

    // DELETE api/asset/{id}   (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await assetService.DeleteAsync(id, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ----------------------------------------------------------------
    //  Dynamic facet search — "eBay-style" filter panel
    //  POST (not GET) because Filters is an arbitrary-length list of
    //  objects that doesn't map cleanly to a query string.
    // ----------------------------------------------------------------

    // POST api/asset/search
    [HttpPost("search")]
    public async Task<IActionResult> Search(AssetSearchRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await assetService.SearchAsync(request);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

        // ----------------------------------------------------------------
    //  Photos (sub-resource)
    // ----------------------------------------------------------------

    // POST api/asset/{id}/photos
    [HttpPost("{id:guid}/photos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PhotoDto>> AddPhoto(Guid id, IFormFile file)
    {
        try
        {
            var photo = await assetService.AddPhotoAsync(id, file, User.GetMemberId().ToString());
            return Ok(photo);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // DELETE api/asset/{id}/photos/{photoId}
    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId)
    {
        try
        {
            await assetService.DeletePhotoAsync(id, photoId, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // PATCH api/asset/{id}/photos/{photoId}/main
    [HttpPatch("{id:guid}/photos/{photoId:guid}/main")]
    public async Task<ActionResult<AssetDetailDto>> SetMainPhoto(Guid id, Guid photoId)
    {
        try
        {
            var asset = await assetService.SetMainPhotoAsync(id, photoId, User.GetMemberId().ToString());
            return Ok(asset);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ConflictException ex)    { return Conflict(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ----------------------------------------------------------------
    //  Maintenance / cost history (sub-resource)
    // ----------------------------------------------------------------

    // GET api/asset/{id}/maintenance-history
    [HttpGet("{id:guid}/maintenance-history")]
       public async Task<IActionResult> GetMaintenanceHistory(Guid id, [FromQuery] PagingParams pagingParams)
       {
        var history = await assetService.GetMaintenanceHistoryAsync(id, pagingParams);
        return Ok(history);
    }

    // GET api/asset/{id}/contracts?page=1&pageSize=5
    [HttpGet("{id:guid}/contracts")]
    public async Task<IActionResult> GetContractHistory(Guid id, [FromQuery] PagingParams pagingParams)
    {
        var history = await assetService.GetContractHistoryAsync(id, pagingParams);
        return Ok(history);
    }

    // POST api/asset/{id}/maintenance-history
    [HttpPost("{id:guid}/maintenance-history")]
    public async Task<IActionResult> AddMaintenanceRecord(Guid id, CostAssetHistCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var record = await assetService.AddMaintenanceRecordAsync(id, dto, User.GetMemberId().ToString());
            return Ok(record);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    
    // PUT api/asset/{id}/maintenance-history/{recordId}
    [HttpPut("{id:guid}/maintenance-history/{recordId:guid}")]
    public async Task<IActionResult> UpdateMaintenanceRecord(Guid id, Guid recordId, CostAssetHistUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var record = await assetService.UpdateMaintenanceRecordAsync(id, recordId, dto, User.GetMemberId().ToString());
            return Ok(record);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ConflictException ex)    { return Conflict(new { message = ex.Message }); }

    }

    // DELETE api/asset/{id}/maintenance-history/{recordId}
    [HttpDelete("{id:guid}/maintenance-history/{recordId:guid}")]
    public async Task<IActionResult> DeleteMaintenanceRecord(Guid id, Guid recordId)
    {
        try
        {
            await assetService.DeleteMaintenanceRecordAsync(id, recordId, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    
    // GET api/asset/{id}/availability?from=2026-02-01&to=2026-02-15
    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> CheckAvailability(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (from >= to) return BadRequest(new { message = "Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη." });
        var result = await assetService.CheckAvailabilityAsync(id, from, to);
        return Ok(result);
    }

    // GET api/asset/calendar?from=2026-01-01&to=2026-12-31
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] AssetCalendarParams p)
    {
        if (p.From >= p.To) return BadRequest(new { message = "Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη." });
        var result = await assetService.GetCalendarAsync(p.From, p.To);
        return Ok(result);
    }

}



