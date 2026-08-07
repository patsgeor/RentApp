namespace API.Helper;

/// <summary>
/// Πόσο συχνά επιτρέπεται να ξανατρέξει το ανά-tenant status refresh
/// (βλ. StatusRefreshMiddleware). Δεν χρειάζεται να είναι μικρό: οι οθόνες
/// υπολογίζουν το «ληξιπρόθεσμο» δηλωτικά, η στήλη ενημερώνεται εδώ μόνο
/// για αναφορές/φίλτρα/ιστορικό.
/// </summary>
public class StatusRefreshSettings
{
    public int IntervalHours { get; set; } = 1;

    public TimeSpan Interval => TimeSpan.FromHours(Math.Max(1, IntervalHours));
}
