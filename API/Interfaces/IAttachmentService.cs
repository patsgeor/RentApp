using API.DTOs.Attachment;

namespace API.Interfaces;

public interface IAttachmentService
{
    Task<List<AttachmentDto>> GetForEntityAsync(string entityType, Guid entityId);
    Task<AttachmentDto> UploadAsync(string entityType, Guid entityId, IFormFile file, string currentUserId);
    Task DeleteAsync(Guid attachmentId, string currentUserId);
}
