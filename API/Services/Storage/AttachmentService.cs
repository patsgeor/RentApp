using API.Data.Contexts;
using API.DTOs.Attachment;
using API.Entities;
using API.Errors;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Services.Storage;

/// <summary>
/// Δικαιολογητικά (αποδείξεις, τιμολόγια, συμβατικά έγγραφα) επισυναπτόμενα σε
/// πάγιο, συμβόλαιο ή πληρωμή/είσπραξη. Χτισμένο πάνω στην ήδη γενική οντότητα
/// FileAttachment — δεν χρειάστηκε καμία αλλαγή σχήματος.
///
/// Ο έλεγχος απομόνωσης ενοίκου γίνεται δύο φορές, σκόπιμα: μία στην ανεύρεση
/// της γονικής οντότητας (μέσω global query filter — αν ανήκει σε άλλον ένοικο,
/// επιστρέφει null και το αίτημα απορρίπτεται σαν να μην υπάρχει) και μία στην
/// ίδια την εγγραφή FileAttachment κατά τη διαγραφή, για τον ίδιο λόγο.
/// </summary>
public class AttachmentService(
    AppDbContext context,
    ITenantProvider tenantProvider,
    IFileStorage storage,
    FileValidationService validator,
    ImageCompressor compressor,
    IOptions<StorageSettings> storageOptions) : IAttachmentService
{
    private readonly StorageSettings _storageSettings = storageOptions.Value;


    public async Task<List<AttachmentDto>> GetForEntityAsync(string entityType, Guid entityId)
    {
        EnsureKnownEntityType(entityType);

        var rows = await context.FileAttachments
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<AttachmentDto> UploadAsync(
        string entityType, Guid entityId, IFormFile file, string currentUserId)
    {
        EnsureKnownEntityType(entityType);
        await EnsureParentBelongsToTenantAsync(entityType, entityId);

        var contentType = await validator.ValidateAsync(file);

        // Έλεγχος πριν αγγίξουμε καν το R2: file.Length είναι το ανώτατο δυνατό
        // (η συμπίεση εικόνων μόνο μικραίνει το τελικό μέγεθος), άρα ασφαλής
        // συντηρητικός έλεγχος πριν σπαταλήσουμε ένα upload που θα απορριφθεί.
        await EnsureQuotaAsync(file.Length);

        await using var source = file.OpenReadStream();

        Stream toUpload = source;
        var finalContentType = contentType;
        var extension = ExtensionFor(contentType);

        // Οι εικόνες συμπιέζονται πριν φύγουν προς το R2· τα PDF μένουν ως έχουν.
        MemoryStream? compressed = null;
        if (compressor.CanCompress(contentType))
        {
            compressed = compressor.Compress(source);
            toUpload = compressed;
            finalContentType = FileValidationService.ContentTypes.Jpeg;
            extension = ".jpg";
        }

        try
        {
            var objectKey = $"{tenantProvider.TenantId}/{entityType}/{entityId}/{Guid.NewGuid()}{extension}";
            var uploadResult = await storage.UploadAsync(toUpload, objectKey, finalContentType);

            var attachment = new FileAttachment
            {
                TenantId    = tenantProvider.TenantId,
                EntityType  = entityType,
                EntityId    = entityId,
                FileName    = file.FileName,
                ContentType = finalContentType,
                FilePath    = objectKey,   // κλειδί αντικειμένου R2, όχι URL — βλ. IFileStorage.ResolveUrl
                PublicId    = objectKey,
                SizeBytes   = uploadResult.SizeBytes, // πραγματικό μέγεθος στο R2 (μετά τη συμπίεση, όχι το αρχικό)
                CreatedBy   = currentUserId
            };

            context.FileAttachments.Add(attachment);
            await context.SaveChangesAsync();

            return ToDto(attachment);
        }
        finally
        {
            compressed?.Dispose();
        }
    }

    public async Task DeleteAsync(Guid attachmentId, string currentUserId)
    {
        // Το global query filter περιορίζει ήδη στον τρέχοντα ένοικο: αν το
        // αναγνωριστικό ανήκει σε άλλον, το FindAsync δεν το βρίσκει καν.
        var attachment = await context.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new NotFoundException($"Το δικαιολογητικό '{attachmentId}' δεν βρέθηκε.");

        await storage.DeleteAsync(attachment.FilePath);

        // Hard delete, όπως ήδη γίνεται στα δικαιολογητικά εξόδων
        // (PaymentRepository.RemoveAttachment) — δεν έχει νόημα soft-delete για
        // αρχείο που μόλις αφαιρέθηκε και από το storage.
        context.FileAttachments.Remove(attachment);
        await context.SaveChangesAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void EnsureKnownEntityType(string entityType)
    {
        if (!AttachmentEntityTypes.All.Contains(entityType))
            throw new BadRequestException(
                $"Άγνωστος τύπος οντότητας '{entityType}'. Επιτρεπτά: {string.Join(", ", AttachmentEntityTypes.All)}.");
    }

    /// <summary>
    /// Επιβεβαιώνει ότι η γονική εγγραφή υπάρχει στον τρέχοντα ένοικο, πριν
    /// επιτραπεί το ανέβασμα. Χωρίς αυτό, θα ήταν δυνατό να επισυναφθεί αρχείο
    /// σε αναγνωριστικό που ανήκει σε άλλη εταιρεία.
    /// </summary>
    private async Task EnsureParentBelongsToTenantAsync(string entityType, Guid entityId)
    {
        var exists = entityType switch
        {
            AttachmentEntityTypes.Asset    => await context.Assets.AnyAsync(a => a.Id == entityId),
            AttachmentEntityTypes.Contract => await context.Contracts.AnyAsync(c => c.Id == entityId),
            AttachmentEntityTypes.Payment  => await context.Payments.AnyAsync(p => p.Id == entityId),
            _                              => false
        };

        if (!exists)
            throw new NotFoundException($"{entityType} '{entityId}' δεν βρέθηκε.");
    }

    /// <summary>
    /// Το free tier του R2 (10GB) είναι σε επίπεδο λογαριασμού Cloudflare, κοινό
    /// για όλους τους ενοίκους — γι' αυτό IgnoreQueryFilters: το άθροισμα πρέπει
    /// να καλύπτει όλα τα αρχεία, όχι μόνο του τρέχοντος tenant.
    /// </summary>
    private async Task EnsureQuotaAsync(long incomingBytes)
    {
        var currentTotal = await context.FileAttachments
            .IgnoreQueryFilters()
            .SumAsync(a => (long?)a.SizeBytes) ?? 0;

        if (currentTotal + incomingBytes > _storageSettings.MaxTotalStorageBytes)
            throw new BadRequestException(
                $"Ο διαθέσιμος αποθηκευτικός χώρος έχει εξαντληθεί (όριο {_storageSettings.MaxTotalStorageGb:0.#}GB). " +
                "Διαγράψτε παλιά αρχεία ή αγοράστε επιπλέον χώρο αποθήκευσης.");
    }

    private AttachmentDto ToDto(FileAttachment a) => new()
    {
        Id          = a.Id,
        FileName    = a.FileName,
        ContentType = a.ContentType,
        Url         = storage.ResolveUrl(a.FilePath) ?? "",
        CreatedAt   = a.CreatedAt
    };

    private static string ExtensionFor(string contentType) => contentType switch
    {
        FileValidationService.ContentTypes.Jpeg => ".jpg",
        FileValidationService.ContentTypes.Png  => ".png",
        FileValidationService.ContentTypes.Webp => ".webp",
        FileValidationService.ContentTypes.Pdf  => ".pdf",
        _                                        => ""
    };
}
