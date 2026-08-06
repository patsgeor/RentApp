import { Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TenantService } from '../../../core/services/tenant-service';
import { TenantAdminDto, SubscriptionStatus, TenantUserDto } from '../../../types/tenant';
import { PlanType } from '../../../types/user';

@Component({
  selector: 'app-tenant-list',
  imports: [DatePipe, CurrencyPipe, FormsModule, RouterLink],
  templateUrl: './tenant-list.html',
})
export class TenantList implements OnInit {
  private svc = inject(TenantService);

  readonly PlanType = PlanType;
  readonly SubscriptionStatus = SubscriptionStatus;

  tenants  = signal<TenantAdminDto[]>([]);
  loading  = signal(false);
  savingId = signal<string | null>(null);
  errorMsg = signal('');

  // Ανάπτυξη γραμμής για προβολή χρηστών του tenant
  expandedId   = signal<string | null>(null);
  users        = signal<TenantUserDto[]>([]);
  usersLoading = signal(false);

  ngOnInit() { this.load(); }

  toggleUsers(t: TenantAdminDto) {
    if (this.expandedId() === t.id) { this.expandedId.set(null); return; }
    this.expandedId.set(t.id);
    this.users.set([]);
    this.usersLoading.set(true);
    this.svc.getUsers(t.id).subscribe({
      next: u  => { this.users.set(u); this.usersLoading.set(false); },
      error: () => { this.usersLoading.set(false); this.errorMsg.set('Σφάλμα φόρτωσης χρηστών.'); }
    });
  }

  load() {
    this.loading.set(true);
    this.errorMsg.set('');
    this.svc.getAll().subscribe({
      next: r => { this.tenants.set(r); this.loading.set(false); },
      error: () => { this.errorMsg.set('Σφάλμα φόρτωσης tenants.'); this.loading.set(false); }
    });
  }

  changePlan(t: TenantAdminDto, event: Event) {
    const planType = Number((event.target as HTMLSelectElement).value) as PlanType;
    if (planType === t.planType) return;
    this.savingId.set(t.id);
    this.errorMsg.set('');
    this.svc.updatePlan(t.id, planType).subscribe({
      next: () => {
        this.tenants.update(list => list.map(x => x.id === t.id ? { ...x, planType } : x));
        this.savingId.set(null);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα ενημέρωσης πλάνου.');
        this.savingId.set(null);
      }
    });
  }

  changeStatus(t: TenantAdminDto, event: Event) {
    const status = Number((event.target as HTMLSelectElement).value) as SubscriptionStatus;
    if (status === t.subscriptionStatus) return;
    this.savingId.set(t.id);
    this.errorMsg.set('');
    this.svc.updateStatus(t.id, status).subscribe({
      next: () => {
        this.tenants.update(list => list.map(x => x.id === t.id ? { ...x, subscriptionStatus: status } : x));
        this.savingId.set(null);
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Σφάλμα ενημέρωσης κατάστασης.');
        this.savingId.set(null);
      }
    });
  }

  planLabel(p: PlanType): string {
    const map: Record<number, string> = {
      [PlanType.Free]:  'Free',
      [PlanType.Basic]: 'Basic',
      [PlanType.Pro]:   'Pro',
    };
    return map[p] ?? '—';
  }

  statusLabel(s: SubscriptionStatus): string {
    const map: Record<number, string> = {
      [SubscriptionStatus.Trial]:     'Δοκιμαστική',
      [SubscriptionStatus.Active]:    'Ενεργή',
      [SubscriptionStatus.Suspended]: 'Ανασταλμένη',
      [SubscriptionStatus.Cancelled]: 'Ακυρωμένη',
    };
    return map[s] ?? '—';
  }

  statusBadge(s: SubscriptionStatus): string {
    const map: Record<number, string> = {
      [SubscriptionStatus.Trial]:     'badge-info',
      [SubscriptionStatus.Active]:    'badge-success',
      [SubscriptionStatus.Suspended]: 'badge-warning',
      [SubscriptionStatus.Cancelled]: 'badge-error',
    };
    return `badge badge-sm ${map[s] ?? ''}`;
  }
}
