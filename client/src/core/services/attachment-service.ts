import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AttachmentDto, AttachmentEntityType } from '../../types/attachment';

@Injectable({ providedIn: 'root' })
export class AttachmentService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}attachment`;

  // Πρέπει να ταιριάζει με API.Services.Reports... όχι, με StorageSettings.MaxUploadMb
  // στο appsettings του server. Ο έλεγχος εδώ είναι μόνο για άμεση ανάδραση στον
  // χρήστη — ο server παραμένει η αυθεντία και ελέγχει ξανά.
  static readonly MAX_UPLOAD_MB = 20;

  getForEntity(entityType: AttachmentEntityType, entityId: string) {
    return this.http.get<AttachmentDto[]>(`${this.base}/${entityType}/${entityId}`);
  }

  upload(entityType: AttachmentEntityType, entityId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<AttachmentDto>(`${this.base}/${entityType}/${entityId}`, form);
  }

  delete(id: string) {
    return this.http.delete(`${this.base}/${id}`);
  }
}
