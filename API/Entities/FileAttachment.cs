using System.ComponentModel.DataAnnotations;

namespace API.Entities;

public class FileAttachment: BaseEntity
{
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = null!;  // π.χ. "Payment", "Invoice"

    [Required]
    public Guid EntityId { get; set; }               // ← αυτό λείπει

    [Required, MaxLength(255)]
    public string FileName { get; set; } = null!;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [Required, MaxLength(1000)]
    public string FilePath { get; set; } = null!;    // Cloudinary URL

    public string? PublicId { get; set; }            // για διαγραφή από Cloudinary
}