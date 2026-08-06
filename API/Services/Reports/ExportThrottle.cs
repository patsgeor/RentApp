using Microsoft.Extensions.Options;

namespace API.Services.Reports;

/// <summary>
/// Περιορίζει τις ταυτόχρονες εξαγωγές. Το ClosedXML κρατά ολόκληρο το workbook
/// στη μνήμη, οπότε πέντε παράλληλα αιτήματα πολλαπλασιάζουν την κατανάλωση και
/// μπορούν να ρίξουν τη διεργασία. Singleton ώστε το φράγμα να είναι καθολικό.
/// </summary>
public sealed class ExportThrottle : IDisposable
{
    private readonly SemaphoreSlim _slots;
    private readonly TimeSpan _wait;

    public ExportThrottle(IOptions<ReportSettings> settings)
    {
        var s = settings.Value;
        _slots = new SemaphoreSlim(Math.Max(1, s.MaxConcurrentExports));
        _wait  = TimeSpan.FromSeconds(Math.Max(1, s.ConcurrencyWaitSeconds));
    }

    /// <summary>Επιστρέφει false αν δεν ελευθερώθηκε θέση εντός του χρονικού ορίου.</summary>
    public Task<bool> TryEnterAsync(CancellationToken ct = default) => _slots.WaitAsync(_wait, ct);

    public void Release() => _slots.Release();

    public void Dispose() => _slots.Dispose();
}
