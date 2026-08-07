using Amazon.S3;
using Amazon.S3.Model;
using API.Helper;
using API.Interfaces;
using Microsoft.Extensions.Options;

namespace API.Services.Storage;

public class R2FileStorage : IFileStorage
{
    private readonly StorageSettings _settings;
    private readonly Lazy<IAmazonS3> _s3;

    // Ο S3 client δημιουργείται με το πρώτο πραγματικό ανέβασμα/διαγραφή/URL
    // αντικειμένου R2, όχι στον constructor. Το IFileStorage κατασκευάζεται σε
    // σχεδόν κάθε αίτημα (μέσω UnitOfWork), και το ResolveUrl καλείται ακόμη και
    // για πάγια χωρίς φωτογραφία· αν η επαλήθευση ρυθμίσεων γινόταν εδώ, η
    // απουσία R2 διαπιστευτηρίων θα έριχνε ολόκληρη την εφαρμογή, όχι μόνο το
    // ανέβασμα αρχείων.
    public R2FileStorage(IOptions<StorageSettings> options)
    {
        _settings = options.Value;
        _s3 = new Lazy<IAmazonS3>(CreateClient);
    }

    private IAmazonS3 CreateClient()
    {
        if (!_settings.IsConfigured)
            throw new InvalidOperationException(
                "Η αποθήκευση R2 δεν έχει ρυθμιστεί πλήρως (AccountId/AccessKey/SecretKey/Bucket).");

        // R2 απαιτεί path-style addressing· χωρίς αυτό οι αιτήσεις πάνε σε
        // υποτομέα (virtual-hosted style) που δεν υποστηρίζεται.
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true
        };

        return new AmazonS3Client(_settings.AccessKey, _settings.SecretKey, config);
    }

    public async Task<FileUploadResult> UploadAsync(
        Stream content, string objectKey, string contentType, CancellationToken ct = default)
    {
        var length = content.Length;

        var request = new PutObjectRequest
        {
            BucketName  = _settings.Bucket,
            Key         = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // Το AWS SDK v4 στέλνει PutObject με chunked streaming ("STREAMING-
            // AWS4-HMAC-SHA256-PAYLOAD[-TRAILER]") από προεπιλογή· το R2 δεν το
            // υποστηρίζει και απαντά με "NotImplemented". Χωρίς αυτό, κανένα
            // upload δεν περνάει ποτέ.
            UseChunkEncoding = false
        };

        await _s3.Value.PutObjectAsync(request, ct);

        return new FileUploadResult(objectKey, contentType, length);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await _s3.Value.DeleteObjectAsync(_settings.Bucket, objectKey, ct);
    }

    public string? ResolveUrl(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue)) return storedValue;

        // Παλαιό απόλυτο URL (Cloudinary, εποχή πριν το R2) — σερβίρεται ως έχει,
        // χωρίς καν να χρειαστεί ο S3 client.
        if (storedValue.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return storedValue;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.Bucket,
            Key        = storedValue,
            Verb       = HttpVerb.GET,
            Expires    = DateTime.UtcNow.AddMinutes(_settings.SignedUrlMinutes)
        };

        return _s3.Value.GetPreSignedURL(request);
    }
}
