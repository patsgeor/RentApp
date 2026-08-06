import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TenantService } from '../../../core/services/tenant-service';
import { AuditLogDto, ErrorLogDto, TenantAdminDto } from '../../../types/tenant';
import { PaginationMetadata } from '../../../types/pagination';

type Tab = 'audit' | 'errors';

@Component({
  selector: 'app-admin-logs',
  imports: [DatePipe, FormsModule],
  templateUrl: './admin-logs.html',
})
export class AdminLogs implements OnInit {
  private svc = inject(TenantService);

  activeTab = signal<Tab>('audit');
  tenants   = signal<TenantAdminDto[]>([]);

  // ── Audit logs ───────────────────────────────────────────────────────
  logs     = signal<AuditLogDto[]>([]);
  metadata = signal<PaginationMetadata | null>(null);
  loading  = signal(false);
  errorMsg = signal('');

  filterTenant = signal('');
  filterAction = signal('');
  filterSearch = signal('');
  page         = signal(1);
  readonly pageSize = 50;

  readonly actions = ['Insert', 'Update', 'Delete'];

  // ── Error logs ───────────────────────────────────────────────────────
  errLogs     = signal<ErrorLogDto[]>([]);
  errMetadata = signal<PaginationMetadata | null>(null);
  errLoading  = signal(false);
  errMsg      = signal('');
  errFilterTenant = signal('');
  errFilterSearch = signal('');
  errPage         = signal(1);
  expandedErrorId = signal<string | null>(null);

  ngOnInit() {
    this.svc.getAll().subscribe({ next: t => this.tenants.set(t) });
    this.load(1);
  }

  switchTab(tab: Tab) {
    this.activeTab.set(tab);
    if (tab === 'errors' && this.errLogs().length === 0) this.loadErrors(1);
  }

  // ── Audit logs ───────────────────────────────────────────────────────
  load(page = this.page()) {
    this.loading.set(true);
    this.errorMsg.set('');
    this.svc.getAuditLogs({
      tenantId: this.filterTenant() || undefined,
      action:   this.filterAction() || undefined,
      search:   this.filterSearch() || undefined,
      page,
      pageSize: this.pageSize,
    }).subscribe({
      next: r => {
        this.logs.set(r.items);
        this.metadata.set(r.metadata);
        this.page.set(r.metadata.currentPage);
        this.loading.set(false);
      },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης logs.'); this.loading.set(false); }
    });
  }

  onFilterChange() { this.load(1); }

  onSearch(e: Event) {
    this.filterSearch.set((e.target as HTMLInputElement).value);
    this.load(1);
  }

  actionBadge(a: string): string {
    const map: Record<string, string> = {
      Insert: 'badge-success',
      Update: 'badge-info',
      Delete: 'badge-error',
    };
    return `badge badge-sm ${map[a] ?? 'badge-ghost'}`;
  }

  actionLabel(a: string): string {
    const map: Record<string, string> = {
      Insert: 'Δημιουργία',
      Update: 'Ενημέρωση',
      Delete: 'Διαγραφή',
    };
    return map[a] ?? a;
  }

  pages(): number[] {
    return this.pageWindow(this.page(), this.metadata()?.totalPages ?? 1);
  }

  // ── Error logs ───────────────────────────────────────────────────────
  loadErrors(page = this.errPage()) {
    this.errLoading.set(true);
    this.errMsg.set('');
    this.svc.getErrorLogs({
      tenantId: this.errFilterTenant() || undefined,
      search:   this.errFilterSearch() || undefined,
      page,
      pageSize: this.pageSize,
    }).subscribe({
      next: r => {
        this.errLogs.set(r.items);
        this.errMetadata.set(r.metadata);
        this.errPage.set(r.metadata.currentPage);
        this.errLoading.set(false);
      },
      error: () => { this.errMsg.set('Σφάλμα φόρτωσης σφαλμάτων.'); this.errLoading.set(false); }
    });
  }

  onErrorFilterChange() { this.loadErrors(1); }

  onErrorSearch(e: Event) {
    this.errFilterSearch.set((e.target as HTMLInputElement).value);
    this.loadErrors(1);
  }

  toggleStackTrace(id: string) {
    this.expandedErrorId.set(this.expandedErrorId() === id ? null : id);
  }

  statusBadge(code: number): string {
    if (code >= 500) return 'badge-error';
    if (code >= 400) return 'badge-warning';
    return 'badge-ghost';
  }

  errorPages(): number[] {
    return this.pageWindow(this.errPage(), this.errMetadata()?.totalPages ?? 1);
  }

  private pageWindow(cur: number, total: number): number[] {
    // Παράθυρο γύρω από την τρέχουσα σελίδα — τα logs μπορεί να είναι χιλιάδες σελίδες
    const from = Math.max(1, cur - 2);
    const to   = Math.min(total, from + 4);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  }
}
