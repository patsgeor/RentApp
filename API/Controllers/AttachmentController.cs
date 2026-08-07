using API.DTOs.Attachment;
using API.Errors;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Δικαιολογητικά (αποδείξεις, τιμολόγια, έγγραφα) για πάγια, συμβόλαια και
/// πληρωμές/εισπράξεις. Το RequestSizeLimit φράσσει υπερμεγέθη αιτήματα πριν
/// καν διαβαστούν — δεύτερος έλεγχος στο FileValidationService επιβεβαιώνει το
/// ίδιο όριο στα bytes που πράγματι ελήφθησαν.
/// </summary>
[Authorize]
public class AttachmentController(IAttachmentService attachmentService) : BaseApiController
{
    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<ActionResult<List<AttachmentDto>>> GetForEntity(string entityType, Guid entityId)
    {
        try
        {
            return Ok(await attachmentService.GetForEntityAsync(entityType, entityId));
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{entityType}/{entityId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)] // λίγο πάνω από το όριο εφαρμογής, ώστε το ίδιο το validator να δώσει το ακριβές μήνυμα
    public async Task<ActionResult<AttachmentDto>> Upload(
        string entityType, Guid entityId, IFormFile file)
    {
        try
        {
            var result = await attachmentService.UploadAsync(entityType, entityId, file, User.GetMemberId());
            return Ok(result);
        }
        catch (NotFoundException ex)   { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await attachmentService.DeleteAsync(id, User.GetMemberId());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
