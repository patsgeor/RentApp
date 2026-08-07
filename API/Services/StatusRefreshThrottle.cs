using System.Collections.Concurrent;
using API.Helper;
using Microsoft.Extensions.Options;

namespace API.Services;

/// <summary>
/// Καθορίζει αν αξίζει να ξανατρέξει το status refresh για συγκεκριμένο tenant,
/// ώστε να μην χτυπάει τη βάση σε κάθε αίτημα. Singleton ώστε το «πότε έτρεξε
/// τελευταία φορά» να είναι κοινό σε όλη τη διεργασία.
/// </summary>
public sealed class StatusRefreshThrottle(IOptions<StatusRefreshSettings> options)
{
    private readonly ConcurrentDictionary<Guid, DateTime> _lastRefreshed = new();
    private readonly TimeSpan _interval = options.Value.Interval;

    public bool ShouldRefresh(Guid tenantId) =>
        !_lastRefreshed.TryGetValue(tenantId, out var last) || DateTime.UtcNow - last > _interval;

    public void MarkRefreshed(Guid tenantId) => _lastRefreshed[tenantId] = DateTime.UtcNow;
}
