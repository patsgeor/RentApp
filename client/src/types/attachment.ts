/** Πρέπει να ταιριάζει με API.DTOs.Attachment.AttachmentEntityTypes. */
export type AttachmentEntityType = 'Asset' | 'Contract' | 'Payment';

export interface AttachmentDto {
  id: string;
  fileName: string;
  contentType?: string | null;
  url: string;
  createdAt: string;
}
