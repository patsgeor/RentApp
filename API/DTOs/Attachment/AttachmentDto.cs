namespace API.DTOs.Attachment;

/// <summary>Οι οντότητες στις οποίες επιτρέπεται να επισυναφθεί δικαιολογητικό.</summary>
public static class AttachmentEntityTypes
{
    public const string Asset    = "Asset";
    public const string Contract = "Contract";
    public const string Payment  = "Payment";

    public static readonly string[] All = [Asset, Contract, Payment];
}

public class AttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public string Url { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
