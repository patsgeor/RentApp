using static API.Entities.Enums;

namespace API.Interfaces;

public interface IPlanGate
{
    /// <summary>Πετάει PlanRestrictionException αν το τρέχον πλάνο του tenant είναι κάτω από το ζητούμενο.</summary>
    Task EnsureAtLeastAsync(PlanType requiredPlan);
}
