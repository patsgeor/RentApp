using API.Interfaces;
using API.Services;

namespace API.Middleware;

/// <summary>
/// Αντικαθιστά το παλιό always-on background service: δεν τρέχει τίποτα αν δεν
/// έρθει αίτημα. Σε κάθε authenticated αίτημα, αν έχει περάσει αρκετή ώρα από
/// το τελευταίο refresh για αυτόν τον tenant (StatusRefreshThrottle), τρέχει
/// ένα ελαφρύ UPDATE πριν προχωρήσει στο controller — ώστε το ίδιο αίτημα να
/// βλέπει ήδη φρέσκες καταστάσεις. Χωρίς claim/authentication δεν κάνει τίποτα,
/// οπότε /health, login και register περνάνε χωρίς να αγγίξουν τον ITenantProvider.
/// </summary>
public class StatusRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider,
        IStatusRefreshService refreshService, StatusRefreshThrottle throttle)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(c => c.Type == "TenantId"))
        {
            var tenantId = tenantProvider.TenantId;
            if (throttle.ShouldRefresh(tenantId))
            {
                await refreshService.RefreshAsync(context.RequestAborted);
                throttle.MarkRefreshed(tenantId);
            }
        }

        await next(context);
    }
}
