using API.Data.Contexts;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services;

/// <summary>
/// Συγχρονίζει τις αποθηκευμένες καταστάσεις με την πραγματικότητα: δόσεις που
/// έληξαν γίνονται «Ληξιπρόθεσμες», συμβόλαια που πέρασαν την ημερομηνία λήξης
/// γίνονται «Ολοκληρωμένα». Τρέχει μέσα σε αίτημα (βλ. StatusRefreshMiddleware),
/// όχι σε background timer — γι' αυτό δεν χρειάζεται IgnoreQueryFilters: το
/// global query filter του AppDbContext ήδη περιορίζει στον τρέχοντα tenant.
/// </summary>
public class TenantStatusRefreshService(AppDbContext context) : IStatusRefreshService
{
    public async Task RefreshAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        await context.Installments
            .Where(i => i.Status == InstallmentStatus.Pending
                     && i.DueDate < now
                     && i.Contract.Status != RentalStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InstallmentStatus.Overdue)
                .SetProperty(i => i.UpdatedAt, now), ct);

        await context.Contracts
            .Where(c => c.Status == RentalStatus.Active && c.EndDate < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, RentalStatus.Completed)
                .SetProperty(c => c.UpdatedAt, now), ct);
    }
}
