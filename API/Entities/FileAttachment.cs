using System.ComponentModel.DataAnnotations;

namespace API.Entities;

public class FileAttachment: BaseEntity
{
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = null!;  // π.χ. "Payment", "Installment"

    [Required]
    public Guid EntityId { get; set; }               // ← αυτό λείπει

    [Required, MaxLength(255)]
    public string FileName { get; set; } = null!;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [Required, MaxLength(1000)]
    public string FilePath { get; set; } = null!;    // Cloudinary URL

    public string? PublicId { get; set; }            // για διαγραφή από Cloudinary

    /// <summary>
    /// Μέγεθος του πραγματικά αποθηκευμένου αντικειμένου (μετά τη συμπίεση για
    /// εικόνες) — όχι το αρχικό μέγεθος του upload. Χρησιμοποιείται για τον
    /// έλεγχο συνολικής χωρητικότητας του R2 (βλ. AttachmentService.EnsureQuotaAsync).
    /// </summary>
    public long SizeBytes { get; set; }
}