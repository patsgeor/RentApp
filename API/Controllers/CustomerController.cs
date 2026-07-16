using System;
using API.DTOs.Contacts;
using API.DTOs.Customer;
using API.Errors;
using API.Extensions;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class CustomerController(ICustomerService customerService) : BaseApiController
{
    // GET api/customer?page=1&pageSize=20&search=acme&showDeleted=active|deleted|all
    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] CustomerParams customerParams)
    {
        var result = await customerService.GetAllAsync(customerParams);
        return Ok(result);
    }

    // POST api/customer/{id}/restore
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<CustomerDto>> Restore(Guid id)
    {
        try
        {
            var restored = await customerService.RestoreAsync(id, User.GetMemberId().ToString());
            return Ok(restored);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }


    // GET api/customer/lookup?search=acme   (autocomplete for Contract creation in name or afm.)
    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup([FromQuery] string? search = null)
    {
        var result = await customerService.GetLookupAsync(search);
        return Ok(result);
    }

    // GET api/customer/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id)
    {
        var customer = await customerService.GetByIdAsync(id);
        return customer == null ? NotFound() : Ok(customer);
    }

    // POST api/customer
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await customerService.CreateAsync(dto, User.GetMemberId().ToString());
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }

    }

    // PUT api/customer/{id}
    // για συγγεκριμένο id, ενημερώνει τα στοιχεία του πελάτη με τα δεδομένα που παρέχονται στο dto.
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, CustomerUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await customerService.UpdateAsync(id, dto, User.GetMemberId().ToString());
            return Ok(updated);
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ConflictException ex)    { return Conflict(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // DELETE api/customer/{id}   (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await customerService.DeleteAsync(id, User.GetMemberId().ToString());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // POST api/customer/{id}/contacts
    [HttpPost("{id:guid}/contacts")]
    public async Task<ActionResult<ContactDto>> AddContact(Guid id, ContactCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var contact = await customerService.AddContactAsync(id, dto, User.GetMemberId().ToString());
            return Ok(contact);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PUT api/customer/{id}/contacts/{contactId}
    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    public async Task<ActionResult<ContactDto>> UpdateContact(Guid id, Guid contactId, ContactCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var contact = await customerService.UpdateContactAsync(id, contactId, dto, User.GetMemberId().ToString());
            return Ok(contact);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ConflictException ex)    { return Conflict(new { message = ex.Message }); }

    }


   
    // DELETE api/customer/{id}/contacts/{contactId}
    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> RemoveContact(Guid id, Guid contactId)
    {
        try
        {
            await customerService.RemoveContactAsync(id, contactId);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // GET api/customer/stats
    [HttpGet("stats")]
    public async Task<ActionResult<CustomerStatsDto>> GetStats()
    {
        var stats = await customerService.GetCustomerStatsAsync(User.GetTenantId()); 
        return Ok(stats);
    }
}
