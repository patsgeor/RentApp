using System;
using System.Net;
using System.Text.Json;
using API.Data.Contexts;
using API.Entities;
using API.Errors;

namespace API.Middleware;

public class ExceptionMiddleware(RequestDelegate next,
ILogger<ExceptionMiddleware> logger,
IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context, AuditDbContext auditContext)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,"{message}", ex.Message);

            //returning response to client type json
            context.Response.ContentType="application/json";

            //setting status code =500
            context.Response.StatusCode= (int)HttpStatusCode.InternalServerError;//=500 error server

            //different response for development and production
             var detail = env.IsDevelopment()
                ? ex.StackTrace
                : "Internal Server Error";
            var message = ex.InnerException?.Message ?? ex.Message;
            var response = new ApiException(context.Response.StatusCode, message, detail);

            // Μόνιμη καταγραφή — χωρίς αυτό οι εξαιρέσεις υπάρχουν μόνο στην κονσόλα
            // του διακομιστή και χάνονται με το επόμενο restart.
            await TryPersistAsync(context, ex, auditContext);

            //serialize response to json και απο PascalCase που έχω στην C#  -> σε camelCase που θέλει το javascript
            var option = new JsonSerializerOptions
            {
                PropertyNamingPolicy=JsonNamingPolicy.CamelCase
            };
            var json=JsonSerializer.Serialize(response,option);

            await context.Response.WriteAsync(json);
        }
    }

    private async Task TryPersistAsync(HttpContext context, Exception ex, AuditDbContext auditContext)
    {
        try
        {
            Guid? tenantId = Guid.TryParse(
                context.User.FindFirst("TenantId")?.Value, out var tid) ? tid : null;
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            auditContext.ErrorLogs.Add(new ErrorLog
            {
                TenantId      = tenantId,
                UserId        = userId,
                Method        = context.Request.Method,
                Path          = context.Request.Path.Value ?? "",
                StatusCode    = context.Response.StatusCode,
                Message       = (ex.InnerException?.Message ?? ex.Message).Length > 500
                                    ? (ex.InnerException?.Message ?? ex.Message)[..500]
                                    : ex.InnerException?.Message ?? ex.Message,
                ExceptionType = ex.GetType().Name,
                // Περικοπή στο όριο της στήλης: χωρίς αυτήν, ένα βαθύ stack trace
                // θα προκαλούσε αποτυχία εισαγωγής και θα χανόταν όλη η καταγραφή.
                StackTrace    = ex.StackTrace is { Length: > 4000 }
                                    ? ex.StackTrace[..4000]
                                    : ex.StackTrace
            });
            await auditContext.SaveChangesAsync();
        }
        catch (Exception persistEx)
        {
            // Ποτέ μην αφήσεις τη μόνιμη καταγραφή να ρίξει τον χειρισμό του αρχικού σφάλματος
            logger.LogError(persistEx, "Failed to persist ErrorLog.");
        }
    }
}
