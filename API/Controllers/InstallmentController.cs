using API.DTOs.Payment;
using API.Errors;
using API.Extensions;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class InstallmentController(IInstallmentService installmentService) : BaseApiController
{
    // POST api/installment/generate/{contractId}
    [HttpPost("generate/{contractId:guid}")]
    public async Task<IActionResult> Generate(Guid contractId)
    {
        try
        {
            await installmentService.GenerateInstallmentsAsync(contractId, User.GetMemberId().ToString());
            return Ok(new { message = "Installments generated successfully." });
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // GET api/installment/contract/{contractId}
    [HttpGet("contract/{contractId:guid}")]
    public async Task<ActionResult<List<InstallmentDto>>> GetByContract(Guid contractId)
    {
        var result = await installmentService.GetByContractAsync(contractId);
        return Ok(result);
    }

    // GET api/installment/overdue?page=1&pageSize=20
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue([FromQuery] PagingParams pagingParams)
    {
        var result = await installmentService.GetOverdueAsync(pagingParams);
        return Ok(result);
    }

    // POST api/installment/auto-match/{paymentId}
    [HttpPost("auto-match/{paymentId:guid}")]
    public async Task<ActionResult<MatchResultDto>> AutoMatch(Guid paymentId)
    {
        try
        {
            var result = await installmentService.AutoMatchAsync(paymentId, User.GetMemberId().ToString());
            return Ok(result);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // POST api/installment/allocate/{paymentId}
    [HttpPost("allocate/{paymentId:guid}")]
    public async Task<IActionResult> AllocateManually(
        Guid paymentId, [FromBody] List<AllocationItemDto> items)
    {
        try
        {
            await installmentService.AllocateManuallyAsync(paymentId, items, User.GetMemberId().ToString());
            return Ok(new { message = "Allocation saved." });
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // DELETE api/installment/allocation/{allocationId}
    [HttpDelete("allocation/{allocationId:guid}")]
    public async Task<IActionResult> Deallocate(Guid allocationId)
    {
        try
        {
            await installmentService.DeallocateAsync(allocationId, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // DELETE api/installment/{invoiceId}/cancel
    [HttpDelete("{invoiceId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invoiceId)
    {
        try
        {
            await installmentService.CancelInstallmentAsync(invoiceId, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }
}