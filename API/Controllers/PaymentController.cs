using API.DTOs.Payment;
using API.Errors;
using API.Extensions;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static API.Entities.Enums;

namespace API.Controllers;

[Authorize]
public class PaymentController(IPaymentService paymentService) : BaseApiController
{
    // GET api/payment/contracts?search=&status=&page=1&pageSize=10
    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(
        [FromQuery] PagingParams pagingParams,
        [FromQuery] RentalStatus? status)
    {
        var result = await paymentService.GetContractsAsync(pagingParams.Search, status, pagingParams);
        return Ok(result);
    }

    // GET api/payment/income?contractId=&page=1&pageSize=10
    [HttpGet("income")]
    public async Task<IActionResult> GetIncome(
        [FromQuery] PagingParams pagingParams,
        [FromQuery] Guid? contractId)
    {
        var result = await paymentService.GetIncomeAsync(pagingParams, contractId);
        return Ok(result);
    }

    // POST api/payment/income
    [HttpPost("income")]
    public async Task<ActionResult<PaymentListItemDto>> RecordIncome(IncomeCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await paymentService.RecordIncomeAsync(dto, User.GetMemberId().ToString());
            return Ok(result);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // GET api/payment/expenses?page=1&pageSize=10
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] PagingParams pagingParams)
    {
        var result = await paymentService.GetExpensesAsync(pagingParams);
        return Ok(result);
    }

    // POST api/payment/expenses   (multipart/form-data so we can attach a file)
    [HttpPost("expenses")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PaymentListItemDto>> RecordExpense(
        [FromForm] ExpenseCreateDto dto,
        IFormFile? file)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await paymentService.RecordExpenseAsync(dto, file, User.GetMemberId().ToString());
            return Ok(result);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // DELETE api/payment/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await paymentService.DeleteAsync(id, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
