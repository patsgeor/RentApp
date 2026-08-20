using static API.Entities.Enums;

namespace API.Errors;

public class ApiException (int statusCode, string message, string? details)
{
    public int StatusCode {get; set;} =statusCode;
    public string Message {get; set;} =message;
    public string? Details {get; set;} =details;
}


// Maps to 404 — used when an entity lookup by Id fails.
public class NotFoundException(string message) : Exception(message);
 
// Maps to 400 — used for business-rule violations (duplicate AFM, restricted delete, etc.)
public class BadRequestException(string message) : Exception(message);
 
 
// Maps to 409 — used when a concurrent update is detected (xmin mismatch)
public class ConflictException(string message) : Exception(message);

// Maps to 403 — the tenant's current plan doesn't allow this action.
public class PlanRestrictionException(string message, PlanType requiredPlan) : Exception(message)
{
    public PlanType RequiredPlan { get; } = requiredPlan;
}
