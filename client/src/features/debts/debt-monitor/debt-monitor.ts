import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InstallmentService } from '../../../core/services/installment-service';
import { InstallmentDto, InstallmentStatus, DebtStatsDto } from '../../../types/installment';

@Component({
  selector: 'app-debt-monitor',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './debt-monitor.html',
})
export class DebtMonitor implements OnInit {
  private svc    = inject(InstallmentService);
  private router = inject(Router);

  readonly IS = InstallmentStatus;
  readonly months = [
    { v: 1,  l: 'Ιανουάριος' }, { v: 2,  l: 'Φεβρουάριος' },
    { v: 3,  l: 'Μάρτιος' },    { v: 4,  l: 'Απρίλιος' },
    { v: 5,  l: 'Μάιος' },      { v: 6,  l: 'Ιούνιος' },
    { v: 7,  l: 'Ιούλιος' },    { v: 8,  l: 'Αύγουστος' },
    { v: 9,  l: 'Σεπτέμβριος'},  { v: 10, l: 'Οκτώβριος' },
    { v: 11, l: 'Νοέμβριος' },  { v: 12, l: 'Δεκέμβριος' },
  ];

  items       = signal<InstallmentDto[]>([]);
  stats       = signal<DebtStatsDto | null>(null);
  loading     = signal(false);
  statsLoading = signal(false);
  notifying   = signal<string | null>(null);
  errorMsg    = signal('');
  successMsg  = signal('');
  currentPage = signal(1);
  totalPages  = signal(1);
  totalCount  = signal(0);
  readonly pageSize = 20;

  filterMonth  = signal<number | ''>(new Date().getMonth() + 1);
  filterYear   = signal<number>(new Date().getFullYear());
  filterStatus = signal<InstallmentStatus | ''>('');
  filterSearch = signal('');

  years = computed(() => {
    const y = new Date().getFullYear();
    return [y - 1, y, y + 1, y + 2];
  });

  ngOnInit() {
    this.loadStats();
    this.load(1);
  }

  private loadStats() {
    this.statsLoading.set(true);
    const month  = this.filterMonth()  !== '' ? Number(this.filterMonth()) : undefined;
    this.svc.getStats(month, this.filterYear()).subscribe({
      next: s  => { this.stats.set(s); this.statsLoading.set(false); },
      error: () => this.statsLoading.set(false)
    });
  }

  load(page = 1) {
    this.loading.set(true);
    this.errorMsg.set('');
    const month  = this.filterMonth()  !== '' ? Number(this.filterMonth()) : undefined;
    const status = this.filterStatus() !== '' ? Number(this.filterStatus()) as InstallmentStatus : undefined;

    this.svc.getDebts({
      pageNumber: page,
      pageSize:   this.pageSize,
      month,
      year:       this.filterYear(),
      status,
      search:     this.filterSearch() || undefined,
    }).subscribe({
      next: r => {
        this.items.set(r.items);
        this.totalPages.set(r.metadata.totalPages);
        this.currentPage.set(r.metadata.currentPage);
        this.totalCount.set(r.metadata.totalCount);
        this.loading.set(false);
      },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης.'); this.loading.set(false); }
    });
  }

  onFilterChange() {
    this.loadStats();
    this.load(1);
  }

  onSearch(e: Event) {
    this.filterSearch.set((e.target as HTMLInputElement).value);
    this.load(1);
  }

  sendEmail(id: string) {
    this.notifying.set(id);
    this.errorMsg.set('');
    this.svc.notifyEmail(id).subscribe({
      next: r => {
        this.successMsg.set(r.message);
        this.notifying.set(null);
        setTimeout(() => this.successMsg.set(''), 4000);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Αδυναμία αποστολής email.');
        this.notifying.set(null);
      }
    });
  }

  viewInstallments(contractId: string) {
    this.router.navigate(['/contracts', contractId, 'installments']);
  }

  pages() { return Array.from({ length: this.totalPages() }, (_, i) => i + 1); }

  statusLabel(s: InstallmentStatus) {
    const map: Record<number, string> = {
      [this.IS.Pending]:       'Εκκρεμής',
      [this.IS.PartiallyPaid]: 'Μερικώς',
      [this.IS.Paid]:          'Εξοφλ.',
      [this.IS.Overdue]:       'Ληξ/θεσμη',
      [this.IS.Cancelled]:     'Ακυρ.',
    };
    return map[s] ?? '—';
  }

  statusBadge(s: InstallmentStatus) {
    const map: Record<number, string> = {
      [this.IS.Pending]:       'badge-warning',
      [this.IS.PartiallyPaid]: 'badge-info',
      [this.IS.Paid]:          'badge-success',
      [this.IS.Overdue]:       'badge-error',
      [this.IS.Cancelled]:     'badge-ghost',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }
}