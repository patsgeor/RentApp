using API.DTOs.Report;
using API.Errors;
using API.Interfaces;
using API.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class ReportController(IReportService reportService, ExportThrottle throttle) : BaseApiController
{
    /// <summary>
    /// Πλήθος γραμμών ανά σύνολο δεδομένων, ώστε ο χρήστης να δει τι θα κατεβάσει
    /// πριν ξεκινήσει η εξαγωγή. Εκτελεί μόνο COUNT — δεν φορτώνει δεδομένα.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ReportPreviewDto>> Preview(ReportRequestDto request)
    {
        try
        {
            return Ok(await reportService.PreviewAsync(request));
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Παράγει το αρχείο Excel. Η «Πλήρης Αναφορά Περιόδου» δεν είναι ξεχωριστό
    /// endpoint — το UI στέλνει το ίδιο αίτημα με όλα τα σύνολα επιλεγμένα, ώστε
    /// να υπάρχει μία μόνο διαδρομή κώδικα προς συντήρηση.
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export(ReportRequestDto request, CancellationToken ct)
    {
        if (!await throttle.TryEnterAsync(ct))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Εκτελούνται ήδη εξαγωγές αυτή τη στιγμή. Δοκιμάστε ξανά σε λίγο." });

        try
        {
            var data = await reportService.LoadAsync(request);

            using var wb = ExcelWorkbookBuilder.Build(data);

            // Γράφεται σε MemoryStream και επιστρέφεται ως FileStreamResult ώστε το
            // ASP.NET Core να το στείλει με streaming στο response. Το ClosedXML δεν
            // υποστηρίζει σταδιακή εγγραφή — το workbook παραμένει στη μνήμη — γι'
            // αυτό η ουσιαστική προστασία είναι το όριο γραμμών του ReportService.
            var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            var name = $"rentapp-report-{request.DateFrom:yyyyMMdd}-{request.DateTo:yyyyMMdd}.xlsx";

            return File(stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                name);
        }
        catch (BadRequestException ex) { return BadRequest(new { message = ex.Message }); }
        finally { throttle.Release(); }
    }
}
