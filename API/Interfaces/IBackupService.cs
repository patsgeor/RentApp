namespace API.Interfaces;

public record BackupResult(MemoryStream Stream, string TenantName);

public interface IBackupService
{
    /// <summary>Χτίζει ένα πλήρες Excel backup όλων των δεδομένων του τρέχοντος tenant.</summary>
    Task<BackupResult> BuildAsync(CancellationToken ct);
}
