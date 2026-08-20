using API.Errors;
using API.Interfaces;
using API.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Πλήρες backup των δεδομένων του tenant, σε ένα κουμπί. Χρησιμοποιεί το ίδιο
/// process-wide ExportThrottle με τις αναφορές — και τα δύο κρατούν ολόκληρο το
/// workbook στη μνήμη μέσω ClosedXML, άρα μοιράζονται τον ίδιο κίνδυνο OOM.
/// </summary>
[Authorize(Policy = "RequireAdminRole")]
public class BackupController(IBackupService backupService, ExportThrottle throttle) : BaseApiController
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!await throttle.TryEnterAsync(ct))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Εκτελούνται ήδη εξαγωγές αυτή τη στιγμή. Δοκιμάστε ξανά σε λίγο." });

        try
        {
            var result = await backupService.BuildAsync(ct);

            var safeName = string.Join("-", result.TenantName.Split(Path.GetInvalidFileNameChars()))
                .Replace(" ", "-").ToLowerInvariant();
            var fileName = $"rentapp-backup-{safeName}-{DateTime.UtcNow:yyyyMMdd}.xlsx";

            return File(result.Stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex)   { return NotFound(new { message = ex.Message }); }
        finally { throttle.Release(); }
    }
}
