using API.DTOs.Contract;
using API.Errors;
using API.Extensions;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class ContractController(IContractService contractService) : BaseApiController
{
    // GET api/contract?pageNumber=1&pageSize=10&search=...&status=1
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ContractParams p)
    {
        var result = await contractService.GetAllAsync(p);
        return Ok(result);
    }

    // GET api/contract/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDetailDto>> GetById(Guid id)
    {
        var dto = await contractService.GetByIdAsync(id);
        return dto == null ? NotFound() : Ok(dto);
    }

    // GET api/contract/available-assets?startDate=...&endDate=...&excludeContractId=...
    [HttpGet("available-assets")]
    public async Task<ActionResult<List<AvailableAssetDto>>> GetAvailableAssets(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? excludeContractId = null)
    {
        if (endDate <= startDate)
            return BadRequest(new { message = "Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη." });

        var assets = await contractService.GetAvailableAssetsAsync(startDate, endDate, excludeContractId);
        return Ok(assets);
    }

    // POST api/contract
    [HttpPost]
    public async Task<ActionResult<ContractDetailDto>> Create(ContractCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await contractService.CreateAsync(dto, User.GetMemberId().ToString());
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // PUT api/contract/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractDetailDto>> Update(Guid id, ContractUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await contractService.UpdateAsync(id, dto, User.GetMemberId().ToString());
            return Ok(updated);
        }
        catch (NotFoundException ex)   { return NotFound(new { message = ex.Message }); }
        catch (ConflictException ex)   { return Conflict(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ΔΕΝ υπάρχει endpoint διαγραφής συμβολαίου — ούτε φυσικής ούτε ήπιας.
    // Ένα αποθηκευμένο συμβόλαιο αποτελεί λογιστικό παραστατικό: συνδέεται με
    // δόσεις, κατανομές πληρωμών και ιστορικό παγίων, οπότε η εξαφάνισή του θα
    // άφηνε τα οικονομικά στοιχεία ασυμφώνητα. Η ματαίωση εκφράζεται ως μεταβολή
    // κατάστασης σε RentalStatus.Cancelled μέσω του PUT παραπάνω, ώστε η εγγραφή
    // να παραμένει ορατή και ελέγξιμη.

    // POST api/contract/{id}/send-email   (multipart/form-data ώστε να δέχεται συνημμένα)
    [HttpPost("{id:guid}/send-email")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ContractEmailResultDto>> SendEmail(
        Guid id, [FromForm] ContractEmailDto dto, [FromForm] List<IFormFile>? files)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var attachments = new List<EmailAttachment>();
            foreach (var file in files ?? [])
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                attachments.Add(new EmailAttachment(
                    file.FileName,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    ms.ToArray()));
            }

            var result = await contractService.SendByEmailAsync(
                id, dto, attachments, User.GetMemberId().ToString(), User.GetEmail());
            return Ok(result);
        }
        catch (NotFoundException ex)   { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)           { return BadRequest(new { message = $"Αποτυχία αποστολής email: {ex.Message}" }); }
    }
}
