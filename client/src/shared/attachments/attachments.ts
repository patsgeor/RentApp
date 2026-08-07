import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AttachmentService } from '../../core/services/attachment-service';
import { AttachmentDto, AttachmentEntityType } from '../../types/attachment';

/**
 * Δικαιολογητικά (αποδείξεις, τιμολόγια, έγγραφα) για πάγιο, συμβόλαιο ή
 * πληρωμή/είσπραξη — επαναχρησιμοποιήσιμο σε τρεις σελίδες.
 *
 * Ο έλεγχος μεγέθους εδώ είναι μόνο άμεση ανάδραση· ο server ελέγχει ξανά με
 * το πραγματικό όριο (appsettings Storage:MaxUploadMb) και είναι η αυθεντία.
 */
@Component({
  selector: 'app-attachments',
  imports: [DatePipe],
  templateUrl: './attachments.html',
})
export class Attachments implements OnInit {
  @Input({ required: true }) entityType!: AttachmentEntityType;
  @Input({ required: true }) entityId!: string;

  private service = inject(AttachmentService);

  readonly maxUploadMb = AttachmentService.MAX_UPLOAD_MB;

  items    = signal<AttachmentDto[]>([]);
  loading  = signal(false);
  uploading = signal(false);
  deleting = signal<string | null>(null);
  errorMsg = signal('');

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.errorMsg.set('');
    this.service.getForEntity(this.entityType, this.entityId).subscribe({
      next: rows => { this.items.set(rows); this.loading.set(false); },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης δικαιολογητικών.'); this.loading.set(false); }
    });
  }

  onFileSelected(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // επιτρέπει να ξαναδιαλεγεί το ίδιο αρχείο αν χρειαστεί
    if (!file) return;

    if (file.size > this.maxUploadMb * 1024 * 1024) {
      this.errorMsg.set(`Το αρχείο (${(file.size / 1024 / 1024).toFixed(1)} MB) ξεπερνά το όριο των ${this.maxUploadMb} MB.`);
      return;
    }

    this.errorMsg.set('');
    this.uploading.set(true);
    this.service.upload(this.entityType, this.entityId, file).subscribe({
      next: dto => { this.items.update(list => [dto, ...list]); this.uploading.set(false); },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα ανεβάσματος αρχείου.');
        this.uploading.set(false);
      }
    });
  }

  remove(id: string) {
    if (!confirm('Διαγραφή δικαιολογητικού;')) return;
    this.deleting.set(id);
    this.service.delete(id).subscribe({
      next: () => { this.items.update(list => list.filter(x => x.id !== id)); this.deleting.set(null); },
      error: () => { this.errorMsg.set('Σφάλμα διαγραφής.'); this.deleting.set(null); }
    });
  }

  isImage(item: AttachmentDto): boolean {
    return !!item.contentType?.startsWith('image/');
  }
}
