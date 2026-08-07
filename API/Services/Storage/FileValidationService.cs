using API.Errors;
using API.Helper;
using Microsoft.Extensions.Options;

namespace API.Services.Storage;

/// <summary>
/// Επικύρωση αρχείων πριν φτάσουν στο storage: μέγεθος και πραγματικός τύπος.
///
/// Ο έλεγχος τύπου γίνεται στα πρώτα bytes του περιεχομένου (magic number) και
/// όχι στο Content-Type του αιτήματος, το οποίο ο πελάτης δηλώνει ελεύθερα —
/// ένα εκτελέσιμο μετονομασμένο σε .pdf θα περνούσε αλλιώς απαρατήρητο.
/// </summary>
public class FileValidationService(IOptions<StorageSettings> settings)
{
    private readonly StorageSettings _settings = settings.Value;

    public static class ContentTypes
    {
        public const string Jpeg = "image/jpeg";
        public const string Png  = "image/png";
        public const string Webp = "image/webp";
        public const string Pdf  = "application/pdf";
    }

    public bool IsImage(string contentType) =>
        contentType is ContentTypes.Jpeg or ContentTypes.Png or ContentTypes.Webp;

    /// <summary>
    /// Ελέγχει μέγεθος και μαγικό αριθμό, και επιστρέφει τον πραγματικό τύπο
    /// περιεχομένου. Πετά BadRequestException με μήνυμα κατάλληλο για εμφάνιση
    /// στον χρήστη αν κάτι δεν περνά.
    /// </summary>
    public async Task<string> ValidateAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new BadRequestException("Το αρχείο είναι κενό.");

        var maxBytes = _settings.MaxUploadMb * 1024L * 1024L;
        if (file.Length > maxBytes)
            throw new BadRequestException(
                $"Το αρχείο ({file.Length / 1024.0 / 1024.0:N1} MB) ξεπερνά το όριο των {_settings.MaxUploadMb} MB.");

        var header = new byte[16];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
            if (read < 4)
                throw new BadRequestException("Το αρχείο είναι πολύ μικρό για να αναγνωριστεί.");
        }

        var detected = Detect(header)
            ?? throw new BadRequestException(
                "Μη αποδεκτός τύπος αρχείου. Επιτρέπονται: JPEG, PNG, WebP, PDF.");

        return detected;
    }

    /// <summary>
    /// Αναγνωρίζει τον τύπο από τα πρώτα bytes. Το WebP χρειάζεται δύο σημεία
    /// ελέγχου (RIFF στην αρχή, WEBP στο byte 8) γιατί το container RIFF
    /// χρησιμοποιείται και από άλλες, μη υποστηριζόμενες μορφές.
    /// </summary>
    private static string? Detect(byte[] header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ContentTypes.Jpeg;

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ContentTypes.Png;

        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ContentTypes.Webp;

        if (header.Length >= 4 &&
            header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46) // %PDF
            return ContentTypes.Pdf;

        return null;
    }
}
