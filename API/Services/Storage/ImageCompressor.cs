using API.Helper;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace API.Services.Storage;

/// <summary>
/// Συμπίεση εικόνων πριν την αποθήκευση: περιορισμός διάστασης και
/// επανακωδικοποίηση. Μια φωτογραφία κινητού μερικών MB καταλήγει τυπικά κάτω
/// από 500 KB — εκεί κερδίζεται ο χώρος του δωρεάν πακέτου R2.
///
/// SkiaSharp (άδεια MIT) αντί για το δημοφιλέστερο ImageSharp, το οποίο από την
/// έκδοση 3 έχει Six Labors Split License: δωρεάν κάτω από όριο ετήσιων εσόδων,
/// επί πληρωμή πάνω από αυτό. Δεδομένου ότι η εφαρμογή προορίζεται για πώληση,
/// προτιμήθηκε βιβλιοθήκη χωρίς καμία τέτοια δέσμευση.
/// </summary>
public class ImageCompressor(IOptions<StorageSettings> settings)
{
    private readonly StorageSettings _settings = settings.Value;

    public bool CanCompress(string contentType) =>
        contentType is FileValidationService.ContentTypes.Jpeg
                     or FileValidationService.ContentTypes.Png
                     or FileValidationService.ContentTypes.Webp;

    /// <summary>
    /// Επιστρέφει συμπιεσμένη έξοδο JPEG. Ο καλών είναι υπεύθυνος να διαθέσει
    /// (dispose) το επιστρεφόμενο stream.
    /// </summary>
    public MemoryStream Compress(Stream input)
    {
        using var original = SKBitmap.Decode(input)
            ?? throw new InvalidOperationException("Αδυναμία αποκωδικοποίησης εικόνας.");

        var (width, height) = Fit(original.Width, original.Height, _settings.MaxImageDimension);
        var needsResize = width != original.Width || height != original.Height;

        // Ξεχωριστές μεταβλητές αντί για κοινή "resized ?? original": δύο using
        // πάνω στο ίδιο αντικείμενο θα το διέθεταν δύο φορές.
        SKBitmap? scaled = needsResize
            ? original.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default)
            : null;

        try
        {
            var source = scaled ?? original;
            using var image = SKImage.FromBitmap(source);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, _settings.ImageQuality);

            var output = new MemoryStream();
            data.SaveTo(output);
            output.Position = 0;
            return output;
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>Υπολογίζει διαστάσεις εντός του ορίου, διατηρώντας τις αναλογίες.</summary>
    private static (int width, int height) Fit(int width, int height, int maxDimension)
    {
        if (width <= maxDimension && height <= maxDimension)
            return (width, height);

        var scale = width >= height
            ? (double)maxDimension / width
            : (double)maxDimension / height;

        return (Math.Max(1, (int)Math.Round(width * scale)),
                Math.Max(1, (int)Math.Round(height * scale)));
    }
}
