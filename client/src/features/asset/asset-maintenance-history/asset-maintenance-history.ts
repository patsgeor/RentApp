import { Component, OnInit, Input, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { AssetService } from '../../../core/services/asset-service';
import { CostAssetHistDto, CostAssetHistCreateDto, CostAssetHistUpdateDto, AssetFinancialSummaryDto } from '../../../types/asset';
import { PaginatedResult, PaginationMetadata } from '../../../types/pagination';

@Component({
  selector: 'app-asset-maintenance-history',
  imports: [ReactiveFormsModule, DatePipe, CurrencyPipe],
  templateUrl: './asset-maintenance-history.html',
})
export class AssetMaintenanceHistory implements OnInit {
  @Input({ required: true }) assetId!: string;

  private fb      = inject(FormBuilder);
  private service = inject(AssetService);
  private editRowVersion = 0;

  isOpen      = signal(false);
  loading     = signal(false);
  saving      = signal(false);
  deleting    = signal<string | null>(null);  // id of record being deleted
  error       = signal('');
  showAddForm = signal(false);
  editingId   = signal<string | null>(null);  // id of record being edited

  records  = signal<CostAssetHistDto[]>([]);
  metadata = signal<PaginationMetadata | null>(null);
  page     = signal(1);
  pageSize = 5;

  // Σύνολα σε ΟΛΕΣ τις εγγραφές (όχι μόνο στην τρέχουσα σελίδα)
  summary = signal<AssetFinancialSummaryDto | null>(null);

  form = this.fb.group({
    date:         [new Date().toISOString().substring(0, 10), Validators.required],
    description:  ['', [Validators.required, Validators.maxLength(250)]],
    cost:         [0 as number, [Validators.required, Validators.min(0)]],
    maintainedBy: ['', Validators.maxLength(100)],
  });

  editForm = this.fb.group({
    date:         ['', Validators.required],
    description:  ['', [Validators.required, Validators.maxLength(250)]],
    cost:         [0 as number, [Validators.required, Validators.min(0)]],
    maintainedBy: ['', Validators.maxLength(100)],
  });

  get f()  { return this.form.controls; }
  get ef() { return this.editForm.controls; }

  ngOnInit() {}

  toggle() {
    this.isOpen.update(v => !v);
    if (this.isOpen() && this.records().length === 0) {
      this.load();
    }
  }

  private loadSummary() {
    this.service.getFinancialSummary(this.assetId).subscribe({
      next: s => this.summary.set(s)
    });
  }

  load(page = this.page()) {
    this.loading.set(true);
    this.error.set('');
    this.loadSummary();
    this.service.getMaintenanceHistory(this.assetId, page, this.pageSize).subscribe({
      next: (result: PaginatedResult<CostAssetHistDto>) => {
        this.records.set(result.items);
        this.metadata.set(result.metadata);
        this.page.set(result.metadata.currentPage);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Σφάλμα φόρτωσης ιστορικού συντηρήσεων.');
        this.loading.set(false);
      }
    });
  }

  goTo(p: number) {
    const meta = this.metadata();
    if (!meta || p < 1 || p > meta.totalPages) return;
    this.load(p);
  }

  get pages(): number[] {
    const total = this.metadata()?.totalPages ?? 0;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  // ── Add ──────────────────────────────────────────────────────────────

  save() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    this.error.set('');
    const dto: CostAssetHistCreateDto = {
      date:         new Date(this.f['date'].value!).toISOString(),
      description:  this.f['description'].value!,
      cost:         +this.f['cost'].value!,
      maintainedBy: this.f['maintainedBy'].value || undefined,
    };
    this.service.addMaintenanceRecord(this.assetId, dto).subscribe({
      next: () => {
        this.showAddForm.set(false);
        this.form.reset({ date: new Date().toISOString().substring(0, 10), cost: 0 });
        this.load(1);
        this.saving.set(false);
      },
      error: () => {
        this.error.set('Σφάλμα αποθήκευσης.');
        this.saving.set(false);
      }
    });
  }

  // ── Edit ─────────────────────────────────────────────────────────────

  startEdit(record: CostAssetHistDto) {
  this.showAddForm.set(false);
  this.editingId.set(record.id);
  this.editRowVersion = record.rowVersion ?? 0;   
  this.editForm.reset({
    date:         new Date(record.date).toISOString().substring(0, 10),
    description:  record.description,
    cost:         record.cost,
    maintainedBy: record.maintainedBy ?? '',
  });
}
  cancelEdit() {
    this.editingId.set(null);
    this.error.set('');
  }

  saveEdit(recordId: string) {
    if (this.editForm.invalid) { this.editForm.markAllAsTouched(); return; }
    this.saving.set(true);
    this.error.set('');
    const dto: CostAssetHistUpdateDto = {
      rowVersion:   this.editRowVersion,                   
      date:         new Date(this.ef['date'].value!).toISOString(),
      description:  this.ef['description'].value!,
      cost:         +this.ef['cost'].value!,
      maintainedBy: this.ef['maintainedBy'].value || undefined,
    };
    this.service.updateMaintenanceRecord(this.assetId, recordId, dto).subscribe({
      next: (updated) => {
        this.records.update(list => list.map(r => r.id === recordId ? updated : r));
        this.editingId.set(null);
        this.saving.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Σφάλμα αποθήκευσης.');   // ← πρόσθεσε err param
        this.saving.set(false);
      }
    });
  }

  // ── Delete ───────────────────────────────────────────────────────────

  confirmDelete(recordId: string) {
    if (!confirm('Διαγραφή εγγραφής συντήρησης;')) return;
    this.deleting.set(recordId);
    this.error.set('');
    this.service.deleteMaintenanceRecord(this.assetId, recordId).subscribe({
      next: () => {
        this.deleting.set(null);
        const meta = this.metadata();
        // if we deleted the last item on a non-first page, go back one page
        const newPage = (this.records().length === 1 && (meta?.currentPage ?? 1) > 1)
          ? (meta!.currentPage - 1)
          : this.page();
        this.load(newPage);
      },
      error: () => {
        this.error.set('Σφάλμα διαγραφής.');
        this.deleting.set(null);
      }
    });
  }
}
