using System;
using System.ComponentModel.DataAnnotations;

namespace API.Entities;

public class FileAttachment: BaseEntity
{
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = null!; // π.χ. "Invoice", "Rental"
    
    [Required]
    public Guid EntityId { get; set; }
    
    [Required, MaxLength(255)]
    public string FileName { get; set; } = null!;
    
    [MaxLength(100)]
    public string? ContentType { get; set; }
    
    [Required, MaxLength(1000)]
    public string FilePath { get; set; } = null!; // S3 / URL
    public string? PublicId { get; set; }

}
