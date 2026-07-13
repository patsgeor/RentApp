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
        await installmentService.GenerateInstallmentsAsync(contractId, User.GetMemberId().ToString());
        return Ok(new { message = "Οι δόσεις δημιουργήθηκαν επιτυχώς." });
    }

    // GET api/installment/contract/{contractId}
    [HttpGet("contract/{contractId:guid}")]
    public async Task<ActionResult<List<InstallmentDto>>> GetByContract(Guid contractId)
    {
        var result = await installmentService.GetByContractAsync(contractId);
        return Ok(result);
    }

    // GET api/installment/debts?month=6&year=2026&status=3&search=...
    [HttpGet("debts")]
    public async Task<IActionResult> GetDebts([FromQuery] DebtParams p)
    {
        var result = await installmentService.GetDebtsAsync(p);
        return Ok(result);
    }

    // GET api/installment/overdue
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue([FromQuery] PagingParams pagingParams)
    {
        var result = await installmentService.GetOverdueAsync(pagingParams);
        return Ok(result);
    }

    // POST api/installment/{id}/notify-email
    [HttpPost("{id:guid}/notify-email")]
    public async Task<IActionResult> NotifyEmail(Guid id)
    {
        await installmentService.NotifyByEmailAsync(id, User.GetMemberId().ToString());
        return Ok(new { message = "Το email υπενθύμισης εστάλη επιτυχώς." });
    }

    // POST api/installment/auto-match/{paymentId}
    [HttpPost("auto-match/{paymentId:guid}")]
    public async Task<ActionResult<MatchResultDto>> AutoMatch(Guid paymentId)
    {
        var result = await installmentService.AutoMatchAsync(paymentId, User.GetMemberId().ToString());
        return Ok(result);
    }

    // POST api/installment/allocate/{paymentId}
    [HttpPost("allocate/{paymentId:guid}")]
    public async Task<IActionResult> AllocateManually(
        Guid paymentId, [FromBody] List<AllocationItemDto> items)
    {
        await installmentService.AllocateManuallyAsync(paymentId, items, User.GetMemberId().ToString());
        return Ok(new { message = "Η κατανομή αποθηκεύτηκε." });
    }

    // DELETE api/installment/allocation/{allocationId}
    [HttpDelete("allocation/{allocationId:guid}")]
    public async Task<IActionResult> Deallocate(Guid allocationId)
    {
        await installmentService.DeallocateAsync(allocationId, User.GetMemberId().ToString());
        return NoContent();
    }

    // DELETE api/installment/{invoiceId}/cancel
    [HttpDelete("{invoiceId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invoiceId)
    {
        await installmentService.CancelInstallmentAsync(invoiceId, User.GetMemberId().ToString());
        return NoContent();
    }

    // PUT api/installment/contract/{contractId}/schedule
    [HttpPut("contract/{contractId:guid}/schedule")]
    public async Task<IActionResult> UpdateSchedule(
        Guid contractId, [FromBody] List<ScheduleInstallmentDto> schedule)
    {
        await installmentService.UpdateScheduleAsync(
            contractId, schedule, User.GetMemberId().ToString());
        return Ok(new { message = "Το πρόγραμμα δόσεων αποθηκεύτηκε." });
    }

    // GET api/installment/stats?month=6&year=2026
    [HttpGet("stats")]
    public async Task<ActionResult<DebtStatsDto>> GetStats(
        [FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await installmentService.GetStatsAsync(month, year);
        return Ok(result);
    }
}