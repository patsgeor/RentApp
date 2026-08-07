namespace API.Helper;

/// <summary>
/// Ρυθμίσεις αποθήκευσης αρχείων (Cloudflare R2 μέσω S3-συμβατού API).
/// Τα κλειδιά έρχονται από μεταβλητές περιβάλλοντος στην παραγωγή και δεν
/// φεύγουν ποτέ προς τον browser — οι πελάτες λαμβάνουν μόνο υπογεγραμμένα URL.
/// </summary>
public class StorageSettings
{
    public string AccountId { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "";

    /// <summary>Μέγιστο μέγεθος ανεβάσματος. Ισχύει πριν από κάθε συμπίεση.</summary>
    public int MaxUploadMb { get; set; } = 20;

    /// <summary>Διάρκεια ισχύος των υπογεγραμμένων συνδέσμων ανάγνωσης.</summary>
    public int SignedUrlMinutes { get; set; } = 30;

    /// <summary>Μέγιστη διάσταση εικόνας μετά τη συμπίεση.</summary>
    public int MaxImageDimension { get; set; } = 1600;

    /// <summary>Ποιότητα επανακωδικοποίησης JPEG (0-100).</summary>
    public int ImageQuality { get; set; } = 80;

    /// <summary>
    /// Ανώτατο συνολικό μέγεθος όλων των αποθηκευμένων αρχείων στο R2 — όλοι οι
    /// ενοίκοι μαζί, γιατί το free tier (10GB) είναι σε επίπεδο λογαριασμού
    /// Cloudflare, όχι ανά εταιρεία. Προεπιλογή 9GB: αφήνει περιθώριο 1GB κάτω
    /// από το όριο των 10GB ώστε να μην ξεκινήσει χρέωση.
    /// </summary>
    public double MaxTotalStorageGb { get; set; } = 9;

    public long MaxTotalStorageBytes => (long)(MaxTotalStorageGb * 1024 * 1024 * 1024);

    public string Endpoint => $"https://{AccountId}.r2.cloudflarestorage.com";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !string.IsNullOrWhiteSpace(Bucket);
}
