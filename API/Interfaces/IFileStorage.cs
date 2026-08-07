namespace API.Interfaces;

public record FileUploadResult(string ObjectKey, string ContentType, long SizeBytes);

/// <summary>
/// Αποθήκευση αρχείων σε Cloudflare R2 (S3-συμβατό), αντικαθιστά το παλιό
/// IPhotoService/Cloudinary. Το bucket είναι ιδιωτικό: κανένα αντικείμενο δεν
/// είναι δημόσια προσβάσιμο, μόνο μέσω των υπογεγραμμένων URL που παράγει αυτή
/// η υπηρεσία — και μόνο αφού ο καλών έχει επιβεβαιώσει ότι ο πόρος ανήκει στον
/// τρέχοντα ένοικο.
/// </summary>
public interface IFileStorage
{
    /// <summary>Ανεβάζει το περιεχόμενο στο δοθέν κλειδί αντικειμένου.</summary>
    Task<FileUploadResult> UploadAsync(
        Stream content, string objectKey, string contentType, CancellationToken ct = default);

    Task DeleteAsync(string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Μετατρέπει μια αποθηκευμένη τιμή σε σύνδεσμο πλοήγησης.
    ///
    /// Οι εγγραφές πριν τη μετάβαση στο R2 έχουν απόλυτο Cloudinary URL· οι νέες
    /// έχουν κλειδί αντικειμένου R2. Η διάκριση γίνεται από το πρόθεμα "http" —
    /// έτσι δεν χρειάζεται μεταφορά (backfill) των παλιών αρχείων: σερβίρονται
    /// από το Cloudinary όσο υπάρχουν, ενώ κάθε νέο ανέβασμα πάει στο R2.
    ///
    /// Καθαρά τοπικός υπολογισμός (υπογραφή HMAC) — δεν είναι async στην πράξη,
    /// αλλά δηλώνεται έτσι ώστε η υλοποίηση να μπορεί να αλλάξει χωρίς να
    /// σπάσει η διεπαφή.
    /// </summary>
    string? ResolveUrl(string? storedValue);
}
