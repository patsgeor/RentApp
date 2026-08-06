import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ReportService } from '../../../core/services/report-service';
import {
  AssetPeriodMode, ReportDataset, ReportPreviewDto, ReportRequestDto
} from '../../../types/report';

interface DatasetOption {
  value: ReportDataset;
  label: string;
  hint: string;
}

@Component({
  selector: 'app-report-export',
  imports: [FormsModule],
  templateUrl: './report-export.html',
})
export class ReportExport {
  private svc = inject(ReportService);

  readonly AssetPeriodMode = AssetPeriodMode;
  readonly ReportDataset = ReportDataset;

  readonly datasetOptions: DatasetOption[] = [
    { value: ReportDataset.Kpi,          label: 'Σύνοψη',     hint: 'Συγκεντρωτικά μεγέθη της περιόδου' },
    { value: ReportDataset.Income,       label: 'Έσοδα',      hint: 'Αναλυτικές εισπράξεις' },
    { value: ReportDataset.Expenses,     label: 'Έξοδα',      hint: 'Αναλυτικές πληρωμές' },
    { value: ReportDataset.Contracts,    label: 'Συμβόλαια',  hint: 'Όσα ήταν ενεργά στην περίοδο' },
    { value: ReportDataset.Assets,       label: 'Πάγια',      hint: 'Με τα δυναμικά χαρακτηριστικά τους' },
    { value: ReportDataset.Installments, label: 'Δόσεις',     hint: 'Με ημερομηνία λήξης στην περίοδο' },
    { value: ReportDataset.Customers,    label: 'Πελάτες',    hint: 'Όσοι καταχωρήθηκαν στην περίοδο' },
  ];

  dateFrom  = signal(this.isoDate(this.startOfMonth()));
  dateTo    = signal(this.isoDate(new Date()));
  assetMode = signal<AssetPeriodMode>(AssetPeriodMode.Registered);
  selected  = signal<Set<ReportDataset>>(new Set(this.datasetOptions.map(o => o.value)));

  preview    = signal<ReportPreviewDto | null>(null);
  previewing = signal(false);
  exporting  = signal(false);
  errorMsg   = signal('');

  /** Διάρκεια περιόδου σε ημέρες — για τον έλεγχο του ορίου των 12 μηνών. */
  private periodDays = computed(() => {
    const from = new Date(this.dateFrom());
    const to   = new Date(this.dateTo());
    return (to.getTime() - from.getTime()) / 86_400_000;
  });

  invalidPeriod = computed(() => this.periodDays() < 0);
  periodTooLong = computed(() => this.periodDays() > 366);
  noneSelected  = computed(() => this.selected().size === 0);

  canSubmit = computed(() =>
    !this.invalidPeriod() && !this.periodTooLong() && !this.noneSelected()
    && !this.previewing() && !this.exporting());

  /** Το κουμπί εξαγωγής μπλοκάρει όταν η προεπισκόπηση δείχνει υπέρβαση ορίου. */
  canExport = computed(() => this.canSubmit() && !(this.preview()?.exceedsLimit ?? false));

  isSelected(d: ReportDataset) { return this.selected().has(d); }

  toggle(d: ReportDataset) {
    this.selected.update(s => {
      const next = new Set(s);
      next.has(d) ? next.delete(d) : next.add(d);
      return next;
    });
    // Η προηγούμενη προεπισκόπηση δεν ισχύει πια για τη νέα επιλογή.
    this.preview.set(null);
  }

  selectAll() { this.selected.set(new Set(this.datasetOptions.map(o => o.value))); this.preview.set(null); }
  selectNone() { this.selected.set(new Set()); this.preview.set(null); }

  setPeriod(kind: 'month' | 'quarter' | 'year') {
    const now = new Date();
    const from = kind === 'month'   ? this.startOfMonth()
               : kind === 'quarter' ? new Date(now.getFullYear(), now.getMonth() - 2, 1)
               :                      new Date(now.getFullYear(), 0, 1);
    this.dateFrom.set(this.isoDate(from));
    this.dateTo.set(this.isoDate(now));
    this.preview.set(null);
  }

  onPeriodChange() { this.preview.set(null); }

  runPreview() {
    if (!this.canSubmit()) return;
    this.previewing.set(true);
    this.errorMsg.set('');

    this.svc.preview(this.request()).subscribe({
      next: p  => { this.preview.set(p); this.previewing.set(false); },
      error: e => { this.errorMsg.set(this.message(e)); this.previewing.set(false); }
    });
  }

  runExport() {
    if (!this.canExport()) return;
    this.exporting.set(true);
    this.errorMsg.set('');

    this.svc.export(this.request()).subscribe({
      next: res => {
        const fallback = `rentapp-report-${this.dateFrom()}-${this.dateTo()}.xlsx`;
        const name = this.svc.filenameFrom(res.headers.get('Content-Disposition'), fallback);
        if (res.body) this.svc.saveBlob(res.body, name);
        this.exporting.set(false);
      },
      error: async e => {
        this.errorMsg.set(await this.blobMessage(e));
        this.exporting.set(false);
      }
    });
  }

  private request(): ReportRequestDto {
    return {
      dateFrom:  this.dateFrom(),
      dateTo:    this.dateTo(),
      datasets:  [...this.selected()],
      assetMode: this.assetMode(),
    };
  }

  private message(err: any): string {
    if (err?.status === 503) return err.error?.message ?? 'Εκτελούνται ήδη εξαγωγές. Δοκιμάστε ξανά σε λίγο.';
    return err?.error?.message ?? 'Σφάλμα κατά την επεξεργασία της αναφοράς.';
  }

  /**
   * Σε αίτημα με responseType 'blob' το σώμα σφάλματος έρχεται κι αυτό ως blob,
   * οπότε το μήνυμα του server πρέπει να διαβαστεί ως κείμενο πριν εμφανιστεί.
   */
  private async blobMessage(err: any): Promise<string> {
    if (err?.error instanceof Blob) {
      try {
        const parsed = JSON.parse(await err.error.text());
        if (parsed?.message) return parsed.message;
      } catch { /* δεν ήταν JSON — πέφτουμε στο γενικό μήνυμα */ }
    }
    return this.message(err);
  }

  private startOfMonth() { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1); }

  /** Τοπική ημερομηνία σε yyyy-MM-dd, χωρίς μετατόπιση ζώνης από το toISOString. */
  private isoDate(d: Date) {
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }
}
