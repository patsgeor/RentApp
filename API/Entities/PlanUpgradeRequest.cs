using System.ComponentModel.DataAnnotations;
using static API.Entities.Enums;

namespace API.Entities;

/// <summary>
/// Αίτημα ενός tenant για αναβάθμιση πλάνου. Δεν αλλάζει τίποτα από μόνο του —
/// το Tenant.PlanType παραμένει ως έχει μέχρι να το εγκρίνει ρητά ο SuperAdmin
/// (βλ. TenantController.ApproveUpgradeRequest). Δεν υπάρχει ακόμα πληρωμή·
/// αυτό είναι απλώς μια ουρά αναμονής.
/// </summary>
public class PlanUpgradeRequest : BaseEntity
{
    public PlanType RequestedPlanType { get; set; }
    public UpgradeRequestStatus Status { get; set; } = UpgradeRequestStatus.Pending;

    [MaxLength(500)]
    public string? Note { get; set; }          // μήνυμα του tenant κατά το αίτημα

    [MaxLength(500)]
    public string? ReviewNote { get; set; }    // σημείωση του SuperAdmin στην έγκριση/απόρριψη
}
