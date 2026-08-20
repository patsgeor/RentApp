using API.Data.Contexts;
using API.Errors;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services;

/// <summary>
/// Ζωντανός έλεγχος πλάνου, όχι από το JWT claim: μια έγκριση αναβάθμισης από
/// τον SuperAdmin πρέπει να ισχύει αμέσως, όχι μετά από έως 30 λεπτά (η νέα
/// διάρκεια του access token). Το κόστος είναι ένα απλό PK lookup, πάνω σε
/// ενέργειες που ήδη γράφουν στη βάση (email, upload, invite) — αμελητέο.
/// </summary>
public class PlanGateService(AppDbContext context, ITenantProvider tenantProvider) : IPlanGate
{
    public async Task EnsureAtLeastAsync(PlanType requiredPlan)
    {
        var currentPlan = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantProvider.TenantId)
            .Select(t => t.PlanType)
            .FirstOrDefaultAsync();

        if (currentPlan < requiredPlan)
            throw new PlanRestrictionException(
                $"Αυτή η ενέργεια απαιτεί πλάνο {requiredPlan} ή ανώτερο. Το τρέχον πλάνο σας είναι {currentPlan}.",
                requiredPlan);
    }
}
