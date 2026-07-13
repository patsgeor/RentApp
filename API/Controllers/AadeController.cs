// API/Controllers/AadeController.cs
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class AadeController(AadeService aadeService) : BaseApiController
{
    [HttpGet("lookup/{afm}")]
    public async Task<IActionResult> Lookup(string afm)
    {
        if (string.IsNullOrWhiteSpace(afm) || afm.Length != 9)
            return BadRequest("Μη έγκυρο ΑΦΜ.");

        var result = await aadeService.LookupByAfmAsync(afm);
        if (result is null)
            return NotFound("Δεν βρέθηκαν στοιχεία για το ΑΦΜ.");

        return Ok(result);
    }
}