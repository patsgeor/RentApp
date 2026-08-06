import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { ReportPreviewDto, ReportRequestDto } from '../../types/report';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}report`;

  /** Πλήθος γραμμών ανά φύλλο, πριν ξεκινήσει η εξαγωγή. */
  preview(req: ReportRequestDto) {
    return this.http.post<ReportPreviewDto>(`${this.base}/preview`, req);
  }

  /**
   * Το αρχείο έρχεται ως blob. Ζητείται και η πλήρης απόκριση ώστε να διαβαστεί
   * το όνομα αρχείου από το Content-Disposition που ορίζει ο server.
   */
  export(req: ReportRequestDto) {
    return this.http.post(`${this.base}/export`, req, {
      responseType: 'blob',
      observe: 'response',
    });
  }

  /**
   * Αποθήκευση blob ως αρχείο. Το objectURL απελευθερώνεται πάντα, αλλιώς το
   * περιεχόμενο μένει δεσμευμένο στη μνήμη του browser μέχρι το reload.
   */
  saveBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    try {
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.click();
    } finally {
      URL.revokeObjectURL(url);
    }
  }

  /** Εξάγει το όνομα αρχείου από το Content-Disposition, με fallback. */
  filenameFrom(disposition: string | null, fallback: string): string {
    if (!disposition) return fallback;
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return match ? decodeURIComponent(match[1]) : fallback;
  }
}
