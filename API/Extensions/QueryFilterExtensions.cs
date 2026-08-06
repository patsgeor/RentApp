using API.Entities;
using static API.Entities.Enums;

namespace API.Extensions;

/// <summary>
/// Κοινοί επιχειρησιακοί κανόνες φιλτραρίσματος, σε ένα σημείο.
///
/// Ο κανόνας «ακυρωμένο συμβόλαιο = εκτός στατιστικών» ήταν διάσπαρτος σε
/// πολλαπλά ερωτήματα. Κάθε νέο σημείο που τον ξεχνούσε παρήγαγε σιωπηλά
/// λανθασμένα οικονομικά μεγέθη, χωρίς κανένα σφάλμα να το προδίδει.
/// Εδώ ορίζεται μία φορά ώστε να μην μπορεί να ξεχαστεί.
/// </summary>
public static class QueryFilterExtensions
{
    /// <summary>Συμβόλαια που μετρούν στα στατιστικά (όχι ακυρωμένα).</summary>
    public static IQueryable<Contract> ExcludeCancelled(this IQueryable<Contract> query)
        => query.Where(c => c.Status != RentalStatus.Cancelled);

    /// <summary>Δόσεις που ανήκουν σε μη ακυρωμένο συμβόλαιο.</summary>
    public static IQueryable<Installment> ExcludeCancelledContracts(this IQueryable<Installment> query)
        => query.Where(i => i.Contract.Status != RentalStatus.Cancelled);

    /// <summary>Αναθέσεις παγίων σε μη ακυρωμένο συμβόλαιο.</summary>
    public static IQueryable<ContractAsset> ExcludeCancelledContracts(this IQueryable<ContractAsset> query)
        => query.Where(ca => ca.Contract.Status != RentalStatus.Cancelled);

    /// <summary>
    /// Δόσεις με ανεξόφλητο υπόλοιπο: ούτε εξοφλημένες ούτε ακυρωμένες, και σε
    /// ενεργό (μη ακυρωμένο) συμβόλαιο. Είναι ο ορισμός της «οφειλής» σε όλη
    /// την εφαρμογή — Πίνακας Ελέγχου, οθόνη Οφειλών και αναφορές πρέπει να
    /// συμφωνούν, αλλιώς ο χρήστης βλέπει δύο διαφορετικά νούμερα.
    /// </summary>
    public static IQueryable<Installment> Outstanding(this IQueryable<Installment> query)
        => query.Where(i => i.Status != InstallmentStatus.Paid
                         && i.Status != InstallmentStatus.Cancelled
                         && i.Contract.Status != RentalStatus.Cancelled);
}
