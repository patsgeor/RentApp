using API.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

/// <summary>
/// Καθαρίζει περιοδικά παλιά AuditLogs/ErrorLogs ώστε να μην μεγαλώνουν απεριόριστα
/// και επιβραδύνουν/γεμίζουν τη βάση. Τρέχει μία φορά στην εκκίνηση και μετά κάθε 24 ώρες.
/// Όρια διατήρησης ρυθμίζονται από το appsettings (Logging:AuditRetentionDays / ErrorRetentionDays).
/// </summary>
public sealed class LogRetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LogRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var auditRetentionDays = configuration.GetValue("Logging:AuditRetentionDays", 90);
        var errorRetentionDays = configuration.GetValue("Logging:ErrorRetentionDays", 90);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

                var auditCutoff = DateTime.UtcNow.AddDays(-auditRetentionDays);
                var errorCutoff = DateTime.UtcNow.AddDays(-errorRetentionDays);

                var deletedAudit = await db.AuditLogs
                    .Where(l => l.Timestamp < auditCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var deletedErrors = await db.ErrorLogs
                    .Where(l => l.Timestamp < errorCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedAudit > 0 || deletedErrors > 0)
                    logger.LogInformation(
                        "LogRetentionService: διαγράφηκαν {AuditCount} audit logs (>{AuditDays}d) και {ErrorCount} error logs (>{ErrorDays}d).",
                        deletedAudit, auditRetentionDays, deletedErrors, errorRetentionDays);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "LogRetentionService: αποτυχία καθαρισμού.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
