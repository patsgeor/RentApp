import { Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ContractService } from '../../../core/services/contract-service';
import { ContractListItemDto, RentalStatus } from '../../../types/contract';

@Component({
  selector: 'app-contract-list',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './contract-list.html',
})
export class ContractList implements OnInit {
  private svc    = inject(ContractService);
  private router = inject(Router);

  readonly RentalStatus = RentalStatus;

  contracts    = signal<ContractListItemDto[]>([]);
  loading      = signal(false);
  errorMsg     = signal('');
  search       = signal('');
  statusFilter = signal<RentalStatus | null>(null);
  currentPage  = signal(1);
  totalPages   = signal(1);
  totalCount   = signal(0);
  readonly pageSize = 10;

  ngOnInit() { this.load(); }

  load(page = 1) {
    this.loading.set(true);
    this.svc.getAll(page, this.pageSize, this.search() || undefined, this.statusFilter() ?? undefined).subscribe({
      next: r => {
        this.contracts.set(r.items);
        this.totalPages.set(r.metadata.totalPages);
        this.currentPage.set(r.metadata.currentPage);
        this.totalCount.set(r.metadata.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    this.load(1);
  }

  onStatus(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.statusFilter.set(v === '' ? null : Number(v) as RentalStatus);
    this.load(1);
  }

  edit(id: string) { this.router.navigate(['/contracts', id, 'edit']); }

  // Δεν υπάρχει διαγραφή συμβολαίου: η ματαίωση γίνεται μέσω επεξεργασίας,
  // θέτοντας την κατάσταση σε «Ακυρωμένο» (βλ. contract-form).

  pages() { return Array.from({ length: this.totalPages() }, (_, i) => i + 1); }

  statusLabel(s: RentalStatus) {
    const map: Record<number, string> = {
      [RentalStatus.Pending]: 'Εκκρεμής', [RentalStatus.Active]: 'Ενεργό',
      [RentalStatus.Completed]: 'Ολοκληρωμένο', [RentalStatus.Cancelled]: 'Ακυρωμένο',
    };
    return map[s] ?? '—';
  }

  statusBadge(s: RentalStatus) {
    const map: Record<number, string> = {
      [RentalStatus.Pending]: 'badge-warning', [RentalStatus.Active]: 'badge-success',
      [RentalStatus.Completed]: 'badge-ghost', [RentalStatus.Cancelled]: 'badge-error',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }
}
